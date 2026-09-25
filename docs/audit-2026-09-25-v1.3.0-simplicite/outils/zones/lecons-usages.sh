#!/usr/bin/env bash
# Compte les occurrences d'identifiants (hors ligne de déclaration) dans src/ (prod) et dans les tests.
cd "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src" || exit 1
ids=(
  _inTransition TIMER_TRANSITION TRANSITION_DURATION_MS PaintTransition PaintLegend IsStep2Key
  CLR_CHAR_ACTIVE CLR_CHAR_ALTGR_ACCENT CLR_KB_BG CLR_BTN_QUIT_TEXT CLR_CHAR_BASE CLR_TRANSITION
  CLR_CTX_TEXT CLR_MOD_ACTIVE CLR_DK_RESULT CLR_STATUS CLR_PROGRESS_TODO CLR_BONUS_TEXT
  _hFontProgress _hFontStatus _hFontBadge _hFontCharDeadKey _hFontInstruction
  ContinueAfterChoice AddContextHighlight AddShiftHighlight AddBackspaceHighlight
  Learning_LegendDeadKey BM_CLICK IDC_BTN_FINISH
  _consecutiveErrors RegisterError MB_ICONINFORMATION CLR_PANEL CLR_BUTTON CLR_BAD
  FindExercise SiteModuleCount SiteLessonCount SiteExerciseCount ResetExercise TypedLine
  ErrorCountsByExpected BackspaceCount CompletedAt StartedAt SuccessfulAttempts HintsUsed
  IsDeadKey MarkCompletedNeutral IsSynthetic OnboardingSyncedMaxStep ExtractCount
  "\.Icon\b" "\.Description\b" _hFontIcon _hFontEmoji _hFontKeyboardTiny LessonsWindowBoundsKey
  CharacterSearch.MethodData LearningTweaks CharOverrides learning-tweaks
  _visible IsVisible CatalogLanguage ChallengeShared ShowChallenge
)
for id in "${ids[@]}"; do
  prod=$(grep -rn --include=*.cs -E "$id" . | grep -v "AZERTYGlobal.Tests" | grep -v "/obj/" | grep -v "/bin/" | wc -l)
  tests=$(grep -rn --include=*.cs -E "$id" AZERTYGlobal.Tests | wc -l)
  echo "$id prod=$prod tests=$tests"
done
