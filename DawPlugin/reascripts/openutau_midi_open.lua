-- OpenUtau Sync MIDI Redirect Script for REAPER
-- When running this action on a selected MIDI item, it checks if the item
-- contains an "OpenUtau Sync" track name marker. If so, it sends a command
-- to OpenUtau to open the track/part editor for that track.
-- If the item is a normal MIDI item, it opens the built-in MIDI editor as usual.
--
-- Usage: Bind this script to the "Media item double click" Mouse Modifier
-- in REAPER's Preferences > Mouse Modifiers.

-- Check if the MIDI take contains an "OpenUtau Sync" track name event (type 3)
function isOpenUtauSyncMidi(take)
  if not take then return false, -1 end
  if not reaper.TakeIsMIDI(take) then return false, -1 end

  local _, _, _, textSysexCount = reaper.MIDI_CountEvts(take)
  local trackNo = -1

  for i = 0, textSysexCount - 1 do
    local rv, _, _, _, evtType, msg = reaper.MIDI_GetTextSysexEvt(take, i)
    if rv and evtType == 3 and msg == "OpenUtau Sync" then
      -- Found our marker! Extract trackNo from velocity of the first note
      local _, noteCount, _, _ = reaper.MIDI_CountEvts(take)
      if noteCount > 0 then
        local _, _, _, _, _, _, _, vel = reaper.MIDI_GetNote(take, 0)
        trackNo = vel - 1 -- velocity is trackNo + 1
      end
      return true, trackNo
    end
  end

  return false, -1
end

-- Send command to OpenUtau to open the part editor for a track
function openPartEditorInOpenUtau(trackNo)
  -- 1. Write command file (using fast io.open first)
  local tempDir = os.getenv("TEMP") or os.getenv("TMP") or (os.getenv("LOCALAPPDATA") .. "\\Temp")
  local cmdFile = tempDir .. "\\openutau_daw_command.txt"
  local f = io.open(cmdFile, "w")
  if f then
    f:write("openPartEditor:" .. tostring(trackNo))
    f:close()
  else
    -- Fallback to shell if io.open is sandboxed
    os.execute('cmd /c echo openPartEditor:' .. tostring(trackNo) .. ' > "' .. cmdFile .. '"')
  end

  -- 2. Bring OpenUtau to foreground (Fastest way without Add-Type)
  -- We use reaper.ExecProcess to avoid console window flashing
  local psCmd = 'powershell -NoProfile -NonInteractive -WindowStyle Hidden -Command "' ..
    "$ws = New-Object -ComObject WScript.Shell; " ..
    "if ($ws.AppActivate('OpenUtau')) { exit }; " ..
    -- If AppActivate fails (e.g. minimized), use a more robust way as fallback
    "$p = Get-Process -Name 'OpenUtau' -ErrorAction SilentlyContinue | Select-Object -First 1; " ..
    "if ($p -and $p.MainWindowHandle -ne 0) { " ..
      "Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;" ..
      "public class W{[DllImport(\"user32.dll\")]public static extern bool SetForegroundWindow(IntPtr h);" ..
      "[DllImport(\"user32.dll\")]public static extern bool ShowWindow(IntPtr h,int n);}'; " ..
      "[W]::ShowWindow($p.MainWindowHandle, 9) | Out-Null; " ..
      "[W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null" ..
    "}" ..
  '"'
  
  if reaper.ExecProcess then
    reaper.ExecProcess(psCmd, 0)
  else
    os.execute(psCmd)
  end

  reaper.ShowConsoleMsg("Sent openPartEditor command for Track #" .. tostring(trackNo) .. "\n")
end

-- Main logic
function main()
  local item = reaper.GetSelectedMediaItem(0, 0)
  if not item then
    reaper.ShowMessageBox("No media item selected.", "OpenUtau", 0)
    return
  end

  local take = reaper.GetActiveTake(item)
  if not take then
    reaper.Main_OnCommand(40153, 0) -- Open built-in MIDI editor
    return
  end

  local isSync, trackNo = isOpenUtauSyncMidi(take)

  if isSync then
    openPartEditorInOpenUtau(trackNo)
  else
    -- Normal MIDI item - open the built-in MIDI editor
    reaper.Main_OnCommand(40153, 0)
  end
end

reaper.Undo_BeginBlock()
main()
reaper.Undo_EndBlock("OpenUtau Sync MIDI Redirect", -1)
