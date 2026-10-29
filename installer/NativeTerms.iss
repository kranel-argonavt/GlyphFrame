{ Nonoperative preview only. The record is user-writable convenience state, not legal proof.
  No command-line/environment/INF acceptance. Never reusable for an operative agreement. }
type
  TNativeSystemTime = record
    Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds: Word;
  end;
  TNativeFileTime = record
    LowDateTime, HighDateTime: LongWord;
  end;

procedure NativeGetSystemTime(var Time: TNativeSystemTime);
  external 'GetSystemTime@kernel32.dll stdcall';
function NativeSystemTimeToFileTime(var Time: TNativeSystemTime; var Value: TNativeFileTime): Boolean;
  external 'SystemTimeToFileTime@kernel32.dll stdcall';
function NativeMoveFileEx(Existing, NewName: String; Flags: LongWord): Boolean;
  external 'MoveFileExW@kernel32.dll stdcall';
function NativeGetFileAttributes(Name: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function NativeGetTempFileName(Path, Prefix: String; Unique: LongWord; Buffer: String): LongWord;
  external 'GetTempFileNameW@kernel32.dll stdcall';

function NativeTermsRecordPath(): String;
begin
  Result := ExpandConstant('{localappdata}\GlyphFrame\native-terms-acknowledgement.txt');
end;

function NativeTermsUnlinked(Path: String): Boolean;
var
  Attributes: LongWord;
begin
  Attributes := NativeGetFileAttributes(Path);
  Result := (Attributes = $FFFFFFFF) or ((Attributes and $400) = 0);
end;

function NativeTermsTimeValid(Value: String): Boolean;
var
  I: Integer;
  Time: TNativeSystemTime;
  FileTime: TNativeFileTime;
begin
  Result := False;
  if Length(Value) <> 24 then Exit;
  if (Value[5] <> '-') or (Value[8] <> '-') or (Value[11] <> 'T') or
     (Value[14] <> ':') or (Value[17] <> ':') or (Value[20] <> '.') or (Value[24] <> 'Z') then Exit;
  for I := 1 to 24 do
    if (I <> 5) and (I <> 8) and (I <> 11) and (I <> 14) and (I <> 17) and (I <> 20) and (I <> 24) then
      if (Value[I] < '0') or (Value[I] > '9') then Exit;
  Time.Year := StrToInt(Copy(Value, 1, 4));
  Time.Month := StrToInt(Copy(Value, 6, 2));
  Time.Day := StrToInt(Copy(Value, 9, 2));
  Time.DayOfWeek := 0;
  Time.Hour := StrToInt(Copy(Value, 12, 2));
  Time.Minute := StrToInt(Copy(Value, 15, 2));
  Time.Second := StrToInt(Copy(Value, 18, 2));
  Time.Milliseconds := StrToInt(Copy(Value, 21, 3));
  Result := NativeSystemTimeToFileTime(Time, FileTime);
end;

function HasCurrentNativeTermsAcknowledgement(): Boolean;
var
  Lines: TArrayOfString;
  Path: String;
  Size: Int64;
begin
  Result := False;
  Path := NativeTermsRecordPath();
  if not NativeTermsUnlinked(ExtractFileDir(Path)) or not NativeTermsUnlinked(Path) then Exit;
  if not FileExists(Path) then Exit;
  if not FileSize64(Path, Size) then Exit;
  if (Size <= 0) or (Size > 1024) then Exit;
  if not LoadStringsFromFile(Path, Lines) then Exit;
  if GetArrayLength(Lines) <> 4 then Exit;
  Result := (Lines[0] = 'GlyphFrame.NativeTermsAcknowledgement.1') and
    (Lines[1] = 'Purpose=ImplementationPreview') and
    (Lines[2] = 'Bundle={#NativeTermsBundle}') and
    (Copy(Lines[3], 1, 16) = 'AcknowledgedUtc=') and
    NativeTermsTimeValid(Copy(Lines[3], 17, 24)) and (Length(Lines[3]) = 40);
end;

function InitializeSetup(): Boolean;
begin
  Result := not WizardSilent or HasCurrentNativeTermsAcknowledgement();
  if not Result then
    Log('Refused unattended new/unacknowledged Microsoft component preview scope. No files or acknowledgement installed.');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpLicense) and HasCurrentNativeTermsAcknowledgement();
end;

procedure NativeNoticesClick(Sender: TObject);
var
  Form: TSetupForm;
  Memo: TRichEditViewer;
  CloseButton: TNewButton;
  Lines: TArrayOfString;
  I: Integer;
begin
  ExtractTemporaryFile('native-component-notices.txt');
  if not LoadStringsFromFile(ExpandConstant('{tmp}\native-component-notices.txt'), Lines) then
    RaiseException('Complete local notices could not be loaded.');
  Form := CreateCustomForm(ScaleX(680), ScaleY(460), False, False);
  try
    Form.Caption := 'GlyphFrame - licenses and source information';
    Form.Position := poScreenCenter;
    CloseButton := TNewButton.Create(Form);
    CloseButton.Parent := Form;
    CloseButton.Align := alBottom;
    CloseButton.Height := ScaleY(32);
    CloseButton.Caption := 'Close';
    CloseButton.ModalResult := mrCancel;
    CloseButton.Cancel := True;
    Memo := TRichEditViewer.Create(Form);
    Memo.Parent := Form;
    Memo.Align := alClient;
    Memo.ReadOnly := True;
    Memo.WordWrap := True;
    Memo.ScrollBars := ssVertical;
    for I := 0 to GetArrayLength(Lines) - 1 do Memo.Lines.Add(Lines[I]);
    Form.ShowModal();
  finally
    Form.Free();
  end;
end;

procedure InitializeWizard();
var
  NoticesButton: TNewButton;
begin
  { Never preload an affirmative choice from wizard state or an INF. }
  WizardForm.LicenseAcceptedRadio.Checked := False;
  WizardForm.LicenseNotAcceptedRadio.Checked := True;
  NoticesButton := TNewButton.Create(WizardForm);
  NoticesButton.Parent := WizardForm.LicensePage;
  NoticesButton.Caption := 'Other licenses and source information';
  NoticesButton.Left := 0;
  NoticesButton.Top := WizardForm.LicenseMemo.Top + WizardForm.LicenseMemo.Height - ScaleY(30);
  NoticesButton.Width := ScaleX(270);
  NoticesButton.Height := ScaleY(26);
  WizardForm.LicenseMemo.Height := WizardForm.LicenseMemo.Height - ScaleY(36);
  NoticesButton.OnClick := @NativeNoticesClick;
end;

function PrepareNativeTermsAcknowledgement(): String;
var
  Path, Temporary, RecordText, Buffer: String;
  Time: TNativeSystemTime;
begin
  Result := '';
  if HasCurrentNativeTermsAcknowledgement() then Exit;
  if WizardSilent then begin
    Result := 'A new Microsoft component preview requires an explicit interactive acknowledgement.';
    Exit;
  end;
  if not WizardForm.LicenseAcceptedRadio.Checked then begin
    Result := 'Review the nonoperative draft and explicitly acknowledge it, or cancel Setup.';
    Exit;
  end;
  Path := NativeTermsRecordPath();
  Temporary := '';
  try
    if not NativeTermsUnlinked(ExtractFileDir(Path)) or not NativeTermsUnlinked(Path) then
      RaiseException('Linked acknowledgement locations are not followed.');
    if not ForceDirectories(ExtractFileDir(Path)) then RaiseException('Application data is not writable.');
    NativeGetSystemTime(Time);
    RecordText := 'GlyphFrame.NativeTermsAcknowledgement.1' + #13#10 +
      'Purpose=ImplementationPreview' + #13#10 + 'Bundle={#NativeTermsBundle}' + #13#10 +
      'AcknowledgedUtc=' + Format('%.4d-%.2d-%.2dT%.2d:%.2d:%.2d.%.3dZ', [Time.Year, Time.Month, Time.Day, Time.Hour, Time.Minute, Time.Second, Time.Milliseconds]) + #13#10;
    Buffer := StringOfChar(#0, 260);
    if NativeGetTempFileName(ExtractFileDir(Path), 'GFT', 0, Buffer) = 0 then
      RaiseException('Cannot create an owned acknowledgement temporary file.');
    Temporary := Copy(Buffer, 1, Pos(#0, Buffer) - 1);
    if not SaveStringToFile(Temporary, AnsiString(RecordText), False) then RaiseException('Cannot write acknowledgement.');
    if not NativeMoveFileEx(Temporary, Path, 9) then RaiseException('Cannot atomically replace acknowledgement.');
    Temporary := '';
    if not HasCurrentNativeTermsAcknowledgement() then RaiseException('Acknowledgement verification failed.');
  except
    Result := 'The preview acknowledgement could not be saved. Installation has not started. Check application-data access and retry, or cancel. ' + GetExceptionMessage();
  end;
  if Temporary <> '' then DeleteFile(Temporary);
end;
