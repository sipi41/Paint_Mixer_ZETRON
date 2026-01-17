using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PaintMixer.Application
{
    
        public enum Dye { Red, Black, White, Yellow, Blue, Green }

        // Internal states (device API maps these to -1/0/1)
        internal enum InternalJobState
        {
            Queued = 0,
            Running = 1,
            Completed = 2,
            Canceled = 3
        }

        public sealed record MixerJob(
            short JobCode,
            IReadOnlyDictionary<Dye, int> Dyes,
            DateTimeOffset CreatedAt
        );

        public sealed class PaintMixerDeviceEmulator : IAsyncDisposable
        {
            private const int MaxActiveJobs = 32;

            private readonly ConcurrentDictionary<short, JobRecord> _jobs = new();
            private readonly Channel<short> _queue;
            private readonly CancellationTokenSource _shutdown = new();
            private readonly Task _worker;

            private int _activeJobs;
            private int _nextJobCode;

            public TimeSpan ProcessingTime { get; init; } = TimeSpan.FromSeconds(15);

            public PaintMixerDeviceEmulator()
            {
                _queue = Channel.CreateUnbounded<short>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

                _worker = Task.Run(WorkerLoopAsync);
            }

            /// <summary>
            /// Returns 16-bit signed job code (0..32767), rolling over to 0 after 32767.
            /// Returns -1 if inputs invalid or capacity exceeded.
            /// </summary>
            public int SubmitJob(
                int red = 0, int black = 0, int white = 0,
                int yellow = 0, int blue = 0, int green = 0)
            {
                var dyes = new Dictionary<Dye, int>
                {
                    [Dye.Red] = red,
                    [Dye.Black] = black,
                    [Dye.White] = white,
                    [Dye.Yellow] = yellow,
                    [Dye.Blue] = blue,
                    [Dye.Green] = green
                };

                if (!ValidateDyes(dyes, out var normalizedNonZero))
                {
                    return -1;
                }

                var newCount = Interlocked.Increment(ref _activeJobs);
                if (newCount > MaxActiveJobs)
                {
                    Interlocked.Decrement(ref _activeJobs);
                    return -1;
                }

                if (!TryAllocateJobCode(out var code))
                {
                    Interlocked.Decrement(ref _activeJobs);
                    return -1;
                }

                var job = new MixerJob(code, normalizedNonZero, DateTimeOffset.UtcNow);
                var rec = new JobRecord(job);

                if (!_jobs.TryAdd(code, rec))
                {
                    Interlocked.Decrement(ref _activeJobs);
                    rec.Dispose();
                    return -1;
                }

                if (!_queue.Writer.TryWrite(code))
                {
                    _jobs.TryRemove(code, out _);
                    Interlocked.Decrement(ref _activeJobs);
                    rec.Dispose();
                    return -1;
                }

                return code;
            }

            /// <summary>
            /// Cancel returns 0 if successful, -1 if failed.
            /// Spec: after cancel, job should behave as "does not exist" to queries.
            /// </summary>
            public int CancelJob(int jobCode)
            {
                if (jobCode < short.MinValue || jobCode > short.MaxValue)
                {
                    return -1;
                }

                var code = (short)jobCode;

                if (!_jobs.TryGetValue(code, out var rec))
                {
                    return -1;
                }

                while (true)
                {
                    var state = rec.State;

                    switch (state)
                    {
                        case InternalJobState.Completed:
                        case InternalJobState.Canceled:
                            return -1;
                    }

                    if (rec.TryTransition(state, InternalJobState.Canceled))
                    {
                        rec.CancelToken.Cancel();

                        Interlocked.Decrement(ref _activeJobs);

                        return 0;
                    }
                }
            }

            /// <summary>
            /// Returns -1 if job does not exist, 0 if queued/running, 1 if completed.
            /// Canceled jobs must appear as non-existent.
            /// </summary>
            public int QueryJobState(int jobCode)
            {
                if (jobCode < short.MinValue || jobCode > short.MaxValue)
                {
                    return -1;
                }

                var code = (short)jobCode;

                if (!_jobs.TryGetValue(code, out var rec))
                {
                    return -1;
                }

                return rec.State switch
                {
                    InternalJobState.Completed => 1,
                    InternalJobState.Queued or InternalJobState.Running => 0,
                    InternalJobState.Canceled => -1, // per spec "does not exist"
                    _ => -1
                };
            }

            /// <summary>Optional debugging helper.</summary>
            public bool TryGetJob(int jobCode, out MixerJob? job, out string state)
            {
                job = null;
                state = "";
                if (jobCode < short.MinValue || jobCode > short.MaxValue)
                {
                    return false;
                }

                var code = (short)jobCode;
                if (!_jobs.TryGetValue(code, out var rec))
                {
                    return false;
                }

                job = rec.Job;
                state = rec.State.ToString();
                return true;
            }

            private async Task WorkerLoopAsync()
            {
                var token = _shutdown.Token;

                try
                {
                    while (await _queue.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                    {
                        while (_queue.Reader.TryRead(out var code))
                        {
                            if (!_jobs.TryGetValue(code, out var rec))
                            {
                                continue;
                            }

                            if (rec.State == InternalJobState.Canceled)
                            {
                                continue;
                            }

                            if (!rec.TryTransition(InternalJobState.Queued, InternalJobState.Running))
                            {
                                continue;
                            }

                            try
                            {
                                await Task.Delay(ProcessingTime, rec.CancelToken.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                continue;
                            }

                            if (rec.State == InternalJobState.Canceled)
                            {
                                continue;
                            }

                            if (rec.TryTransition(InternalJobState.Running, InternalJobState.Completed))
                            {
                                Interlocked.Decrement(ref _activeJobs);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal shutdown
                }
            }

            private bool TryAllocateJobCode(out short code)
            {
                const int maxProbes = 1024;

                for (int i = 0; i < maxProbes; i++)
                {
                    var next = Interlocked.Increment(ref _nextJobCode) - 1; // start at 0
                    var candidate = next % (short.MaxValue + 1);            // 0..32767
                    var s = (short)candidate;

                    if (!_jobs.ContainsKey(s))
                    {
                        code = s;
                        return true;
                    }
                }

                code = 0;
                return false;
            }

            private static bool ValidateDyes(
                Dictionary<Dye, int> dyes,
                out IReadOnlyDictionary<Dye, int> normalizedNonZero)
            {
                normalizedNonZero = new Dictionary<Dye, int>();

                foreach (var amount in dyes.Values)
                {
                    if (amount < 0 || amount > 100)
                        return false;
                }

                var total = dyes.Values.Sum();
                if (total > 100)
                {
                    return false;
                }

                normalizedNonZero = dyes
                    .Where(kvp => kvp.Value != 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                return true;
            }

            public async ValueTask DisposeAsync()
            {
                _shutdown.Cancel();
                _queue.Writer.TryComplete();

                try { await _worker.ConfigureAwait(false); } catch { /* ignore */ }

                foreach (var kvp in _jobs)
                {
                    kvp.Value.Dispose();
                }

                _shutdown.Dispose();
            }

            private sealed class JobRecord(MixerJob job) : IDisposable
            {
                private int _state = (int)InternalJobState.Queued;

                public MixerJob Job { get; } = job;
                public CancellationTokenSource CancelToken { get; } = new();

                public InternalJobState State => (InternalJobState)Volatile.Read(ref _state);

                public bool TryTransition(InternalJobState expected, InternalJobState next)
                {
                    return Interlocked.CompareExchange(
                               ref _state,
                               (int)next,
                               (int)expected)
                           == (int)expected;
                }

                public void Dispose()
                {
                    try { CancelToken.Cancel(); } catch { /* ignore */ }
                    CancelToken.Dispose();
                }
            }
        }

    
}
