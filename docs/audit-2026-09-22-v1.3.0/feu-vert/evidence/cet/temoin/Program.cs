using System;
using System.Runtime.CompilerServices;
using System.Threading;

// Témoin CET par étapes : chaque étape s'annonce sur stderr avant de s'exécuter,
// pour localiser un arrêt brutal. Étape 0 : aucune exception. 1 : un throw/catch
// local. 2 : throw profond, finally, filtre. 3 : idem sur un thread secondaire.
static class Program
{
    static int finallies;

    static void Say(string s) { Console.Error.WriteLine(s); Console.Error.Flush(); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Deep(int n)
    {
        try
        {
            if (n == 0) throw new InvalidOperationException("profond");
            return Deep(n - 1) + 1;
        }
        finally
        {
            Interlocked.Increment(ref finallies);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Round(int depth, int count)
    {
        int caught = 0;
        for (int i = 0; i < count; i++)
        {
            try { Deep(depth); }
            catch (InvalidOperationException e) when (e.Message.Length > 0) { caught++; }
        }
        return caught;
    }

    static int Main()
    {
        Say("etape 0 : demarrage sans exception");
        Say("etape 1 : throw/catch local");
        int local = 0;
        try { throw new InvalidOperationException("local"); }
        catch (InvalidOperationException) { local = 1; }
        Say("etape 1 ok=" + local);
        Say("etape 2 : throw profond + finally + filtre");
        int main = Round(12, 500);
        Say("etape 2 ok=" + main);
        Say("etape 3 : thread secondaire");
        int worker = 0;
        var t = new Thread(() => worker = Round(30, 500));
        t.Start();
        t.Join();
        Say("etape 3 ok=" + worker);
        bool ok = local == 1 && main == 500 && worker == 500 && finallies == 500 * 13 + 500 * 31;
        Say("fin ok=" + ok + " finallies=" + finallies);
        return ok ? 0 : 1;
    }
}
