using Xunit;

// The Diagnostics tests measure the log channel's timing and rely on its drain task starting promptly;
// the hosting tests run a real worker thread and spin-wait. Run in parallel they starve each other
// on xunit's worker threads and the timing assertions become flaky. The whole assembly runs in well
// under a second, so parallelism buys nothing here.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
