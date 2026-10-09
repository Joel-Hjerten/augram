; JoelArt | Converted to AHK v2.0

if !A_IsAdmin {
    Run('*RunAs "' A_ScriptFullPath '"')
    ExitApp()
}

#Requires AutoHotkey v2.0
#SingleInstance Force
;#Persistent
SetKeyDelay(-1)
SendMode("Input")
#HotIf WinActive("ahk_exe blender.exe")

; -------------------------
; SPACE TAP vs HOLD
; -------------------------
$Space::
{
    start := A_TickCount            ; measure current time.
    KeyWait("Space")                ; wait for Space to be released.
    duration := A_TickCount - start ; calculate if Space was held for less than ### ms.
    if (duration < 180) {           ; if so
        SendEvent("{Space}")        ; send Space else send nothing.
    }
return
}


; -------------------------
; LEFT MOUSE
; -------------------------
$LButton::
{
if GetKeyState("Space", "P") {
    Send("{MButton Down}")
    KeyWait("LButton")
    Send("{MButton Up}")
} else {
    Click("Down")
    KeyWait("LButton")
    Click("Up")
}
return
}


; -------------------------
; RIGHT MOUSE
; -------------------------
$RButton::
{
if GetKeyState("Space", "P") {
    SendEvent("+{MButton Down}")
    KeyWait("RButton")
    SendEvent("+{MButton Up}")
} else {
    Click("Down", "Right")
    KeyWait("RButton")
    Click("Up", "Right")
}
return
}


; -------------------------
; MIDDLE MOUSE
; -------------------------
$MButton::
{
if GetKeyState("Space", "P") {
    SendEvent("^{MButton Down}")
} else {
    Click("Down", "Middle")
}
return
}

MButton Up::
{
SendEvent("{MButton Up}")
return
}


; -------------------------
; TRANSFORM SHORTCUTS
; -------------------------
$w::
{
if GetKeyState("Space", "P")
    SendEvent("{g}")
else
    SendEvent("{w}")
return
}

$e::
{
if GetKeyState("Space", "P")
    SendEvent("{s}")
else
    SendEvent("{e}")
return
}

$r::
{
if GetKeyState("Space", "P")
    SendEvent("+^!{r}")
else
    SendEvent("{r}")
return
}

$f::
{
if GetKeyState("Space", "P")
    SendEvent("{NumpadDot}")
else
    SendEvent("{f}")
return
}

