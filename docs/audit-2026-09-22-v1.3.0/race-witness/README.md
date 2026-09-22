# Témoin isolé — course foreground A→B→A

Ce projet d'audit reproduit sans hook réel la différence entre le mock de la suite principale et `RealWin32Api` : les inspections du processus et du champ sensible relisent chacune `GetForegroundWindow`.

Commande :

```powershell
dotnet test .\RaceWitness.csproj -c Release --nologo
```

Résultat attendu au HEAD audité : trois tests passent (A stable est bien suspendue,
B stable reste normale et le scénario A→B→A est réellement traversé) ; le test de
propriété de sécurité échoue. La fenêtre A est `valorant.exe`, mais le snapshot lui
associe le processus B (`notepad.exe`) et `GetEmitContext` autorise le mode `Default`
lorsque A revient au premier plan.

Le témoin est volontairement rouge. Il ne modifie aucun fichier de production et n'installe aucun hook Windows.

Sur ce poste, Windows Application Control refuse de charger une DLL depuis `docs/`
(`0x800711C7`). VSTest rend alors `0` tout en indiquant qu'aucun test n'est disponible :
ce résultat n'est pas un succès. Le même source a donc été injecté dans le projet de tests
existant pour une invocation seulement, sans modifier `src/` :

```powershell
$witness = (Resolve-Path .\docs\audit-2026-09-22-v1.3.0\race-witness\AuditWitness.targets).Path
dotnet test .\src\TypingEngine.Windows.Tests\TypingEngine.Windows.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~ForegroundAbaRaceWitnessTests" "-p:CustomAfterMicrosoftCommonTargets=$witness"
```

Le résultat observé et conservé est dans `RESULT.md`. Après la mesure, le projet de tests
partagé a été reconstruit sans cette cible ; ses huit `ShellRaceSuspensionTests` repassent.
