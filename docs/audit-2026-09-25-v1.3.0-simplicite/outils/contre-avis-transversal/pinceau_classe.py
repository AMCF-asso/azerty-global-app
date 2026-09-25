# Experience hors app : une classe de fenetre dont hbrBackground est un pinceau de l'instance.
# 1) UnregisterClassW supprime-t-il le pinceau (doc WNDCLASSEXW) ?
# 2) Une 2e fenetre creee sur une classe restee enregistree avec un pinceau deja supprime plante-t-elle ?
# Fenetres jamais montrees, aucun hook, aucun registre.
import ctypes
from ctypes import wintypes as w
u = ctypes.WinDLL("user32", use_last_error=True)
g = ctypes.WinDLL("gdi32", use_last_error=True)
k = ctypes.WinDLL("kernel32", use_last_error=True)
WNDPROC = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, w.HWND, w.UINT, w.WPARAM, w.LPARAM)
class WNDCLASSEXW(ctypes.Structure):
    _fields_ = [("cbSize", w.UINT), ("style", w.UINT), ("lpfnWndProc", WNDPROC), ("cbClsExtra", ctypes.c_int),
                ("cbWndExtra", ctypes.c_int), ("hInstance", w.HINSTANCE), ("hIcon", w.HICON), ("hCursor", w.HANDLE),
                ("hbrBackground", w.HBRUSH), ("lpszMenuName", w.LPCWSTR), ("lpszClassName", w.LPCWSTR), ("hIconSm", w.HICON)]
u.DefWindowProcW.restype = ctypes.c_ssize_t
u.DefWindowProcW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
u.CreateWindowExW.restype = w.HWND
u.CreateWindowExW.argtypes = [w.DWORD, w.LPCWSTR, w.LPCWSTR, w.DWORD, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int, w.HWND, w.HMENU, w.HINSTANCE, w.LPVOID]
u.RegisterClassExW.restype = w.ATOM
u.UnregisterClassW.argtypes = [w.LPCWSTR, w.HINSTANCE]
u.DestroyWindow.argtypes = [w.HWND]
u.RedrawWindow.argtypes = [w.HWND, w.LPVOID, w.HANDLE, w.UINT]
g.CreateSolidBrush.restype = w.HBRUSH
g.DeleteObject.argtypes = [w.HANDLE]
g.GetObjectType.argtypes = [w.HANDLE]
k.GetModuleHandleW.restype = w.HINSTANCE
proc = WNDPROC(lambda h, m, wp, lp: u.DefWindowProcW(h, m, wp, lp))
hinst = k.GetModuleHandleW(None)

def register(name, brush):
    wc = WNDCLASSEXW(ctypes.sizeof(WNDCLASSEXW), 0, proc, 0, 0, hinst, None, None, brush, None, name, None)
    return u.RegisterClassExW(ctypes.byref(wc))

def make(name):
    return u.CreateWindowExW(0, name, "t", 0x00CF0000, 0, 0, 200, 200, None, None, hinst, None)

# Essai 1 : le motif des 8 fenetres, sans DeleteObject applicatif
b = g.CreateSolidBrush(0x00F3F3F3)
register("AuditPinceau1", b)
h = make("AuditPinceau1")
u.DestroyWindow(h)
print("E1 type avant Unregister =", g.GetObjectType(b), "(5 = OBJ_BRUSH)")
print("E1 Unregister =", u.UnregisterClassW("AuditPinceau1", hinst))
print("E1 type apres Unregister =", g.GetObjectType(b), "(0 = handle invalide : Windows l'a supprime)")
print("E1 DeleteObject applicatif apres Unregister =", g.DeleteObject(b), "(0 = double suppression refusee)")

# Essai 2 : classe NON desinscrite, pinceau supprime, 2e instance
b2 = g.CreateSolidBrush(0x00F3F3F3)
register("AuditPinceau2", b2)
h1 = make("AuditPinceau2"); u.DestroyWindow(h1)
g.DeleteObject(b2)
print("E2 re-Register (classe deja la) =", register("AuditPinceau2", g.CreateSolidBrush(0)), "err", ctypes.get_last_error())
h2 = make("AuditPinceau2")
print("E2 2e CreateWindowExW =", bool(h2))
print("E2 RedrawWindow(RDW_ERASE|INVALIDATE|ERASENOW|UPDATENOW) =", u.RedrawWindow(h2, None, None, 0x0004 | 0x0001 | 0x0200 | 0x0100))
u.DestroyWindow(h2)
print("E2 Unregister =", u.UnregisterClassW("AuditPinceau2", hinst))
print("fin sans plantage")
