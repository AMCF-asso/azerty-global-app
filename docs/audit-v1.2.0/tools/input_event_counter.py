"""Compte les événements clavier sans enregistrer les touches ni leur contenu."""

from __future__ import annotations

import argparse
import ctypes
import json
import threading
import time
from ctypes import wintypes


WH_KEYBOARD_LL = 13
HC_ACTION = 0
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
WM_SYSKEYDOWN = 0x0104
WM_SYSKEYUP = 0x0105
WM_QUIT = 0x0012
LLKHF_INJECTED = 0x10
KEYEVENTF_KEYUP = 0x0002
VK_F24 = 0x87


class KbdLlHookStruct(ctypes.Structure):
    _fields_ = [
        ("vk_code", wintypes.DWORD),
        ("scan_code", wintypes.DWORD),
        ("flags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("extra_info", ctypes.c_size_t),
    ]


class KeyboardInput(ctypes.Structure):
    _fields_ = [
        ("vk", wintypes.WORD),
        ("scan", wintypes.WORD),
        ("flags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("extra_info", ctypes.c_size_t),
    ]


class MouseInput(ctypes.Structure):
    _fields_ = [
        ("dx", wintypes.LONG),
        ("dy", wintypes.LONG),
        ("mouse_data", wintypes.DWORD),
        ("flags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("extra_info", ctypes.c_size_t),
    ]


class InputUnion(ctypes.Union):
    _fields_ = [("keyboard", KeyboardInput), ("mouse", MouseInput)]


class Input(ctypes.Structure):
    _anonymous_ = ("value",)
    _fields_ = [("type", wintypes.DWORD), ("value", InputUnion)]


LowLevelKeyboardProc = ctypes.WINFUNCTYPE(
    wintypes.LPARAM, ctypes.c_int, wintypes.WPARAM, wintypes.LPARAM
)


def count_events(
    duration_seconds: float, self_test_events: int = 0
) -> dict[str, int | float]:
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

    user32.SetWindowsHookExW.argtypes = [
        ctypes.c_int,
        LowLevelKeyboardProc,
        wintypes.HINSTANCE,
        wintypes.DWORD,
    ]
    user32.SetWindowsHookExW.restype = wintypes.HHOOK
    user32.CallNextHookEx.argtypes = [
        wintypes.HHOOK,
        ctypes.c_int,
        wintypes.WPARAM,
        wintypes.LPARAM,
    ]
    user32.CallNextHookEx.restype = wintypes.LPARAM
    user32.UnhookWindowsHookEx.argtypes = [wintypes.HHOOK]
    user32.UnhookWindowsHookEx.restype = wintypes.BOOL
    user32.GetMessageW.argtypes = [
        ctypes.POINTER(wintypes.MSG),
        wintypes.HWND,
        wintypes.UINT,
        wintypes.UINT,
    ]
    user32.GetMessageW.restype = wintypes.BOOL
    user32.PostThreadMessageW.argtypes = [
        wintypes.DWORD,
        wintypes.UINT,
        wintypes.WPARAM,
        wintypes.LPARAM,
    ]
    user32.PostThreadMessageW.restype = wintypes.BOOL
    user32.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(Input), ctypes.c_int]
    user32.SendInput.restype = wintypes.UINT
    kernel32.GetCurrentThreadId.restype = wintypes.DWORD
    counters = {
        "physicalKeyDown": 0,
        "physicalKeyUp": 0,
        "injectedKeyDown": 0,
        "injectedKeyUp": 0,
    }
    hook_handle = wintypes.HHOOK()

    @LowLevelKeyboardProc
    def callback(code: int, message: int, data_pointer: int) -> int:
        if code == HC_ACTION:
            event = ctypes.cast(
                data_pointer, ctypes.POINTER(KbdLlHookStruct)
            ).contents
            injected = bool(event.flags & LLKHF_INJECTED)
            if message in (WM_KEYDOWN, WM_SYSKEYDOWN):
                counters["injectedKeyDown" if injected else "physicalKeyDown"] += 1
            elif message in (WM_KEYUP, WM_SYSKEYUP):
                counters["injectedKeyUp" if injected else "physicalKeyUp"] += 1
        return user32.CallNextHookEx(hook_handle, code, message, data_pointer)

    hook_handle = user32.SetWindowsHookExW(
        WH_KEYBOARD_LL, callback, None, 0
    )
    if not hook_handle:
        raise ctypes.WinError(ctypes.get_last_error())

    thread_id = kernel32.GetCurrentThreadId()
    timer = threading.Timer(
        duration_seconds,
        lambda: user32.PostThreadMessageW(thread_id, WM_QUIT, 0, 0),
    )
    timer.daemon = True
    self_test_timer = None
    self_test_sent = [0]
    self_test_error = [0]
    if self_test_events:
        def send_self_test() -> None:
            inputs = (Input * (self_test_events * 2))()
            for index in range(self_test_events):
                inputs[index * 2].type = 1
                inputs[index * 2].keyboard = KeyboardInput(VK_F24, 0, 0, 0, 0)
                inputs[index * 2 + 1].type = 1
                inputs[index * 2 + 1].keyboard = KeyboardInput(
                    VK_F24, 0, KEYEVENTF_KEYUP, 0, 0
                )
            self_test_sent[0] = user32.SendInput(
                len(inputs), inputs, ctypes.sizeof(Input)
            )
            self_test_error[0] = ctypes.get_last_error()

        self_test_timer = threading.Timer(0.1, send_self_test)
        self_test_timer.daemon = True

    started = time.perf_counter()
    timer.start()
    if self_test_timer:
        self_test_timer.start()
    try:
        message = wintypes.MSG()
        while user32.GetMessageW(ctypes.byref(message), None, 0, 0) > 0:
            pass
    except KeyboardInterrupt:
        pass
    finally:
        timer.cancel()
        if self_test_timer:
            self_test_timer.cancel()
        user32.UnhookWindowsHookEx(hook_handle)

    return {
        "durationSeconds": round(time.perf_counter() - started, 3),
        "selfTestEventsSent": self_test_sent[0] // 2,
        "selfTestError": self_test_error[0],
        **counters,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=float, default=30.0)
    parser.add_argument("--self-test-events", type=int, default=0)
    args = parser.parse_args()
    if args.seconds <= 0 or args.seconds > 300:
        parser.error("--seconds doit être compris entre 0 et 300")
    if args.self_test_events < 0 or args.self_test_events > 1000:
        parser.error("--self-test-events doit être compris entre 0 et 1000")
    print(
        json.dumps(
            count_events(args.seconds, args.self_test_events),
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
