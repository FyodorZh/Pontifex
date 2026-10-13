using NUnit.Framework;

// The suite drives shared, process-wide state (MemoryRental.Shared, static type-id maps) and contains
// timing-sensitive lifecycle tests, so it must not run in parallel. NumberOfTestWorkers=0 in
// test.runsettings reinforces this; the attribute makes the intent explicit and local to the code.
[assembly: NonParallelizable]
