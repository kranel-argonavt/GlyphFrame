using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class ReleaseNoticesTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-record")]
    [InlineData("wrong-state")]
    [InlineData("future-schema")]
    [InlineData("approval-claim")]
    [InlineData("changed-directions")]
    [InlineData("missing-guide")]
    [InlineData("unsafe-path")]
    [InlineData("duplicate-document")]
    public void ReleaseSchemaPreservesInertNoticeIntegrity(string fault)
    {
        string directory = Path.Combine(Path.GetTempPath(), "GlyphFrame-release-notices-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            JsonArray files = [];
            foreach (string relative in new[] { "LICENSE", "THIRD_PARTY_NOTICES.md", "licenses/FFmpeg/SOURCE-DIRECTIONS.txt", "licenses/FFmpeg/SOURCE-GUIDE.md" })
            {
                string text = "Synthetic inert release text: " + relative;
                string target = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, text);
                files.Add(new JsonObject
                {
                    ["Path"] = relative,
                    ["TextSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()
                });
            }
            JsonObject manifest = new()
            {
                ["SchemaVersion"] = 2,
                ["ReleasePreparation"] = new JsonObject { ["State"] = "ReleasePrepared" },
                ["Files"] = files
            };
            switch (fault)
            {
                case "missing-record": manifest.Remove("ReleasePreparation"); break;
                case "wrong-state": manifest["ReleasePreparation"]!["State"] = "Approved"; break;
                case "future-schema": manifest["SchemaVersion"] = 3; break;
                case "approval-claim": manifest["PublicDistributionApproved"] = true; break;
                case "changed-directions": File.AppendAllText(Path.Combine(directory, "licenses/FFmpeg/SOURCE-DIRECTIONS.txt"), "changed"); break;
                case "missing-guide": File.Delete(Path.Combine(directory, "licenses/FFmpeg/SOURCE-GUIDE.md")); break;
                case "unsafe-path": files[2]!["Path"] = "licenses/../LICENSE"; break;
                case "duplicate-document": files.Add(files[2]!.DeepClone()); break;
            }
            Directory.CreateDirectory(Path.Combine(directory, "licenses"));
            File.WriteAllText(Path.Combine(directory, "licenses/manifest.json"), manifest.ToJsonString());
            ThirdPartyNoticesService service = new(directory, (_, _) => throw new InvalidOperationException("No UI expected"));
            if (fault == "valid")
            {
                string text = service.ReadDocuments();
                Assert.Contains("===== licenses/FFmpeg/SOURCE-DIRECTIONS.txt =====", text);
                Assert.Contains("===== licenses/FFmpeg/SOURCE-GUIDE.md =====", text);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => service.ReadDocuments());
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
