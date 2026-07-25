// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using Robust.Shared;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;

namespace Content.Benchmarks;

/// <summary>
///     Benchmarks the atmos hot loops (Share/CompareExchange/React) across varying gas "sparsity" —
///     how many of the loaded gas types actually have nonzero moles in a mixture. This is what the
///     GasMixture.GetPresenceMask() skip-optimization targets: cost should shrink as sparsity
///     increases (fewer gas types present) and roughly match pre-optimization cost when every gas
///     type is present (worst case, no gas type gets skipped).
/// </summary>
[Virtual]
public class GasMixtureAtmosBenchmark
{
    private TestPair _pair = default!;
    private AtmosphereSystem _atmos = default!;

    private TileAtmosphere _receiverTile = default!;
    private TileAtmosphere _sharerTile = default!;
    private GasMixture _sample = default!;
    private GasMixture _otherSample = default!;
    private GasMixture _reactMixture = default!;
    private TileAtmosphere _reactHolder = default!;

    [Params(1, 4, 13)]
    public int GasCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ProgramShared.PathOffset = "../../../../";
        PoolManager.Startup();

        _pair = PoolManager.GetServerClient().GetAwaiter().GetResult();
        var server = _pair.Server;

        _atmos = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        server.WaitPost(BuildTestData).Wait();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _pair.DisposeAsync();
        PoolManager.Shutdown();
    }

    // Share() and React() can mutate their inputs (diffusion moves moles; a reaction that happens to
    // pass every check would consume/produce moles). Rebuilding fresh data every iteration keeps each
    // measured iteration working from the same starting point instead of drifting as the mixtures
    // equalize/react over repeated calls. IterationSetup time isn't counted in the benchmark result.
    [IterationSetup]
    public void ResetTestData()
    {
        BuildTestData();
    }

    private void BuildTestData()
    {
        var gasCount = Math.Min(GasCount, Atmospherics.TotalNumberOfGases);

        var receiverMix = BuildMixture(gasCount, 40f);
        receiverMix.Temperature = 293.15f;
        var sharerMix = BuildMixture(gasCount, 20f);
        sharerMix.Temperature = 283.15f;

        // TileAtmosphere.Air/AirArchived are [Access]-restricted to AtmosphereSystem et al.,
        // so temperature must be set on the GasMixture before the tile clones it into AirArchived.
        _receiverTile = new TileAtmosphere(default, default, receiverMix);
        _sharerTile = new TileAtmosphere(default, default, sharerMix);

        _sample = BuildMixture(gasCount, 40f);
        _otherSample = BuildMixture(gasCount, 20f);

        _reactMixture = BuildMixture(gasCount, 40f);
        _reactHolder = new TileAtmosphere(default, default, _reactMixture);
    }

    private static GasMixture BuildMixture(int gasCount, float molesPerGas)
    {
        var mixture = new GasMixture(Atmospherics.CellVolume);
        for (var i = 0; i < gasCount; i++)
        {
            mixture.SetMoles(i, molesPerGas);
        }
        return mixture;
    }

    [Benchmark]
    public float Share()
    {
        return _atmos.Share(_receiverTile, _sharerTile, 4);
    }

    [Benchmark]
    public AtmosphereSystem.GasCompareResult CompareExchange()
    {
        return _atmos.CompareExchange(_sample, _otherSample);
    }

    [Benchmark]
    public ReactionResult React()
    {
        return _atmos.React(_reactMixture, _reactHolder);
    }
}
