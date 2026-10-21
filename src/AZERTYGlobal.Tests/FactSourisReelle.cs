using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Levée par un témoin de la vraie souris quand la mesure est impossible : pas de bureau
/// d'entrée (session déconnectée), ou souris en main pendant toute la fenêtre de mesure. Le test
/// est alors rapporté sauté, avec cette raison, et jamais réussi : rien n'a été prouvé.
/// </summary>
public sealed class MesureImpossibleException : Exception
{
    public MesureImpossibleException(string reason) : base(reason) { }
}

/// <summary>
/// Fait qui a besoin de la vraie souris : une <see cref="MesureImpossibleException"/> le rend
/// sauté au lieu d'échoué. xunit 2.6 n'a pas de saut à l'exécution ; ce discoverer le donne
/// sans nouveau paquet. Toute autre exception reste un échec.
/// </summary>
[XunitTestCaseDiscoverer("AZERTYGlobal.Tests.FactSourisReelleDiscoverer", "AZERTYGlobal.Tests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FactSourisReelleAttribute : FactAttribute
{
}

public sealed class FactSourisReelleDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public FactSourisReelleDiscoverer(IMessageSink diagnosticMessageSink) =>
        _diagnosticMessageSink = diagnosticMessageSink;

    public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod, IAttributeInfo factAttribute)
    {
        yield return new SourisReelleTestCase(_diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod);
    }
}

public sealed class SourisReelleTestCase : XunitTestCase
{
    [Obsolete("Réservé à la désérialisation de xunit", error: true)]
    public SourisReelleTestCase() { }

    public SourisReelleTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod) { }

    public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus,
        object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
    {
        var bus = new SautBus(messageBus);
        var summary = await base.RunAsync(diagnosticMessageSink, bus, constructorArguments, aggregator, cancellationTokenSource);
        summary.Failed -= bus.Sautes;
        summary.Skipped += bus.Sautes;
        return summary;
    }

    /// <summary>Change l'échec dû à une mesure impossible en saut, raison comprise.</summary>
    private sealed class SautBus : IMessageBus
    {
        private static readonly string Marqueur = typeof(MesureImpossibleException).FullName!;
        private readonly IMessageBus _inner;

        public SautBus(IMessageBus inner) => _inner = inner;

        public int Sautes { get; private set; }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            if (message is ITestFailed failed && failed.ExceptionTypes.FirstOrDefault() == Marqueur)
            {
                Sautes++;
                return _inner.QueueMessage(new TestSkipped(failed.Test, failed.Messages.FirstOrDefault()));
            }
            return _inner.QueueMessage(message);
        }

        public void Dispose() => _inner.Dispose();
    }
}
