"""Correction de syntaxe du filtre A-11 (un seul motif au lieu de « (motif) or ex is … »)."""
import pathlib, sys
sys.stdout.reconfigure(encoding="utf-8")
p = pathlib.Path("D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/UsageStats.cs")
raw = p.read_bytes()
old = (b"        catch (Exception ex) when ((ex is IOException and not FileNotFoundException and not DirectoryNotFoundException)\r\n"
       b"                                   or ex is UnauthorizedAccessException)\r\n")
new = (b"        catch (Exception ex) when (ex is UnauthorizedAccessException\r\n"
       b"                                   or (IOException and not FileNotFoundException and not DirectoryNotFoundException))\r\n")
if raw.count(old) != 1:
    sys.exit(f"ARRÊT : ancre trouvée {raw.count(old)} fois")
out = raw.replace(old, new)
p.write_bytes(out)
c = out.count(b"\r\n")
print("OK ; CRLF", c, "LF", out.count(b"\n") - c)
