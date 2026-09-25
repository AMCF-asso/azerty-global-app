#!/usr/bin/env bash
# Comptages de la zone lecons : nombres magiques de mise en page, objets GDI par repeint,
# invalidations, blocs de code.
cd "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src" || exit 1
for f in LearningModule.cs LessonsWindow.cs; do
  echo "== $f"
  echo "S(litteral) : $(grep -o -E '\bS\([0-9]+\)' "$f" | wc -l)"
  echo "S(litteral) distincts : $(grep -o -E '\bS\([0-9]+\)' "$f" | sort -u | wc -l)"
  echo "InvalidateRect : $(grep -c 'InvalidateRect' "$f")"
  echo "CreateSolidBrush : $(grep -c 'CreateSolidBrush' "$f")"
  echo "CreatePen : $(grep -c 'CreatePen' "$f")"
  echo "CreateFontW : $(grep -c 'CreateFontW' "$f")"
  echo "SetTimer : $(grep -c 'SetTimer' "$f")"
  echo "KillTimer : $(grep -c 'KillTimer' "$f")"
  echo "const uint CLR_ : $(grep -c -E 'const uint CLR_' "$f")"
  echo "private bool _ (champs bool) : $(grep -c -E '^\s*private bool _' "$f")"
  echo "methodes (private|public|internal ... ( ) : $(grep -c -E '^\s*(private|public|internal)( static)? [A-Za-z<>\[\]?,() ]+ [A-Z][A-Za-z0-9]*\(' "$f")"
done
echo "== KeyboardRenderer.cs CreateSolidBrush/CreatePen"
grep -c -E 'CreateSolidBrush|CreatePen' KeyboardRenderer.cs
echo "== GdiHelpers FillSolidRect impl"
grep -n -A8 'static void FillSolidRect' GdiHelpers.cs
grep -n -A12 'static void DrawPanel' GdiHelpers.cs
