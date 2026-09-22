# Résultat mesuré

Date : 2026-09-22  
HEAD audité : `67601c1b36c6d6a11147b09d04037de488afba1b`

Le témoin a été inclus pour une invocation dans `src/TypingEngine.Windows.Tests` au
moyen de `AuditWitness.targets`, car Windows Application Control refuse de charger une
DLL de test depuis `docs/` (`0x800711C7`). Aucun fichier de `src/` n'a été modifié.

Résultat VSTest :

```text
Failed! - Failed: 1, Passed: 3, Skipped: 0, Total: 4

ForegroundAbaRaceWitnessTests.Positive_control_stable_anti_cheat_A_is_suspended
Passed

ForegroundAbaRaceWitnessTests.Negative_control_stable_normal_B_remains_default
Passed

ForegroundAbaRaceWitnessTests.Witness_traverse_exactly_A_then_B_then_A_during_recompute
Passed

ForegroundAbaRaceWitnessTests.Security_property_anti_cheat_A_must_remain_suspended_after_ABA
Assert.Equal() Failure: Values differ
Expected: DisabledAntiCheat
Actual:   Default
ForegroundAbaRaceWitnessTests.cs:line 78
```

Les deux contrôles stables prouvent que le nom `valorant.exe` choisi pour A est reconnu
anti-cheat et suspendu, tandis que B reste en mode normal. Le troisième test garantit que
les trois lectures A→B→A sont réellement consommées. Le dernier montre que
`GetEmitContext` accepte ensuite le mode calculé depuis B alors que la fenêtre A est
revenue au premier plan.

Le fichier TRX complet est conservé dans `evidence/foreground-aba-witness.trx`.
