using System;
using NUnit.Framework;
using Wargon.Nukecs;

/// <summary>
/// NUKECS_WORKERS=n runs the whole assembly with n scheduler workers (0 = every job runs on the
/// thread that makes it ready). Outside any namespace, so it runs before every fixture.
/// </summary>
[SetUpFixture]
public sealed class NukecsDotNetSetup
{
    [OneTimeSetUp]
    public void ConfigureWorkers()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("NUKECS_WORKERS"), out var count))
            NJobs.SetWorkerCount(count);
        TestContext.Progress.WriteLine($"Nukecs scheduler: {NJobs.WorkerCount} workers");
    }
}
