
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SpatialAudioRadar;

public sealed class AudioCaptureService : IDisposable
{
    private WasapiRecorder? _recorder;

    public bool IsRunning => _recorder != null;

    public async Task StartAsync(int processId)
    {
        await StopAsync();

        var builder = new WasapiRecorderBuilder();

        if (processId == 0)
        {
            // Capture the entire default output device mix.
            _recorder = builder
                .WithLoopbackCapture()
                .Build();
        }
        else
        {
            // Capture only this process tree.
            _recorder = await Task.Run(() =>
                builder
                    .WithProcessLoopback(
                        (uint)processId,
                        ProcessLoopbackMode.IncludeTargetProcessTree)
                    .BuildAsync());
        }

        var recorder = _recorder;
        var format = recorder.WaveFormat;

        recorder.DataAvailable +=
            (buffer, flags, devicePosition, qpcPosition) =>
            {
                SpatialAnalyzer.Process(buffer, format);
            };

        recorder.StartRecording();
    }

    public Task StopAsync()
    {
        if (_recorder != null)
        {
            var old = _recorder;
            _recorder = null;

            try
            {
                old.StopRecording();
            }
            finally
            {
                old.Dispose();
            }
        }

        AudioState.Level = 0;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
