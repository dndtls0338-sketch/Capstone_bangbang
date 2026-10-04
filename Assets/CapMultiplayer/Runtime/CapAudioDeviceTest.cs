using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Cap.Multiplayer
{
    // Local Windows device diagnostics. No file recording, echo, or network transmission.
    // Match Vivox's effective endpoint ID exactly; never silently test a different device.
    public sealed class CapAudioDeviceTest : IDisposable
    {
        public bool IsInput { get; private set; }
        public bool Running => handle != IntPtr.Zero;
        public float Level { get; private set; }
        public int BuffersRead { get; private set; }
        private IntPtr handle;
        private readonly List<Buffer> buffers = new List<Buffer>();
        private readonly short[] samples = new short[1024];
        private const int Rate = 48000;
        private const uint Done = 1;
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WaveFormat
        {
            public ushort format, channels;
            public uint rate, bytesPerSecond;
            public ushort blockAlign, bits, extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr data;
            public uint length, recorded;
            public IntPtr user;
            public uint flags, loops;
            public IntPtr next, reserved;
        }
        private sealed class Buffer
        {
            public IntPtr Data, Header;
            public bool Prepared;
            public Buffer(int bytes)
            {
                Data = Marshal.AllocHGlobal(bytes);
                Header = Marshal.AllocHGlobal((int)HeaderSize);
                Marshal.StructureToPtr(new WaveHeader { data = Data, length = (uint)bytes }, Header, false);
            }
            public void Free() { Marshal.FreeHGlobal(Header); Marshal.FreeHGlobal(Data); }
        }
        private static WaveFormat Format(ushort channels) => new WaveFormat
        { format = 1, channels = channels, rate = Rate, bytesPerSecond = (uint)(Rate * channels * 2), blockAlign = (ushort)(channels * 2), bits = 16 };
        private static void Check(uint result) { if (result != 0) throw new InvalidOperationException("Windows audio error " + result); }

        public static uint ResolveDevice(bool input, string endpointId)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (string.IsNullOrEmpty(endpointId)) throw new InvalidOperationException("Audio device is not ready.");
            uint count = input ? waveInGetNumDevs() : waveOutGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                IntPtr size = Marshal.AllocHGlobal(4), name = IntPtr.Zero;
                try
                {
                    Marshal.WriteInt32(size, 0);
                    // DRV_QUERYFUNCTIONINSTANCEIDSIZE = DRV_RESERVED + 18; ID = +17.
                    uint result = input ? waveInMessage((IntPtr)i, 0x0812, size, IntPtr.Zero) : waveOutMessage((IntPtr)i, 0x0812, size, IntPtr.Zero);
                    if (result != 0) continue;
                    int bytes = Marshal.ReadInt32(size);
                    if (bytes <= 2 || bytes > 32768) continue;
                    name = Marshal.AllocHGlobal(bytes);
                    result = input ? waveInMessage((IntPtr)i, 0x0811, name, (IntPtr)bytes) : waveOutMessage((IntPtr)i, 0x0811, name, (IntPtr)bytes);
                    if (result == 0 && string.Equals(Marshal.PtrToStringUni(name), endpointId, StringComparison.OrdinalIgnoreCase)) return i;
                }
                finally { if (name != IntPtr.Zero) Marshal.FreeHGlobal(name); Marshal.FreeHGlobal(size); }
            }
            throw new InvalidOperationException("The selected audio endpoint is unavailable.");
#else
            throw new PlatformNotSupportedException("Device diagnostics currently support Windows builds.");
#endif
        }

        public void StartInput(string endpointId)
        {
            Dispose(); IsInput = true; BuffersRead = 0;
            try
            {
                uint device = ResolveDevice(true, endpointId);
                var format = Format(1);
                Check(waveInOpen(out handle, device, ref format, IntPtr.Zero, IntPtr.Zero, 0));
                for (int i = 0; i < 4; i++)
                {
                    var buffer = new Buffer(samples.Length * 2); buffers.Add(buffer);
                    Check(waveInPrepareHeader(handle, buffer.Header, HeaderSize)); buffer.Prepared = true;
                    Check(waveInAddBuffer(handle, buffer.Header, HeaderSize));
                }
                Check(waveInStart(handle));
            }
            catch { Dispose(); throw; }
        }

        public void StartOutput(string endpointId, float gain)
        {
            Dispose(); IsInput = false;
            try
            {
                uint device = ResolveDevice(false, endpointId);
                var format = Format(2);
                Check(waveOutOpen(out handle, device, ref format, IntPtr.Zero, IntPtr.Zero, 0));
                short[] tone = CreateTone(gain);
                var buffer = new Buffer(tone.Length * 2); buffers.Add(buffer);
                Marshal.Copy(tone, 0, buffer.Data, tone.Length);
                Check(waveOutPrepareHeader(handle, buffer.Header, HeaderSize)); buffer.Prepared = true;
                Check(waveOutWrite(handle, buffer.Header, HeaderSize));
            }
            catch { Dispose(); throw; }
        }

        // Two gentle tones, with short fades to prevent clicks. Gain respects game/voice volume.
        public static short[] CreateTone(float gain)
        {
            var data = new short[Rate * 2];
            double volume = Math.Max(0, Math.Min(CapVoiceRange.MaximumVoiceVolume, gain)) * .15;
            for (int frame = 0; frame < Rate; frame++)
            {
                double t = frame / (double)Rate, segment = t < .5 ? t : t - .5;
                double fade = Math.Max(0, Math.Min(1, Math.Min(segment / .02, (.4 - segment) / .02)));
                short sample = (short)(Math.Sin(2 * Math.PI * (t < .5 ? 523.25 : 659.25) * t) * volume * fade * short.MaxValue);
                data[frame * 2] = data[frame * 2 + 1] = sample;
            }
            return data;
        }

        // RMS mapped from -60 dBFS .. 0 dBFS to a readable 0..1 meter (not a voice-activity threshold).
        public static float Measure(short[] data, int count)
        {
            if (count <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < count; i++) { double sample = data[i] / 32768.0; sum += sample * sample; }
            double rms = Math.Sqrt(sum / count);
            return rms <= .001 ? 0 : (float)Math.Max(0, Math.Min(1, (20 * Math.Log10(rms) + 60) / 60));
        }

        public void Poll()
        {
            if (!Running) return;
            if (!IsInput)
            {
                if ((Marshal.PtrToStructure<WaveHeader>(buffers[0].Header).flags & Done) != 0) Dispose();
                return;
            }
            foreach (var buffer in buffers)
            {
                var header = Marshal.PtrToStructure<WaveHeader>(buffer.Header);
                if ((header.flags & Done) == 0) continue;
                int count = Math.Min(samples.Length, (int)header.recorded / 2);
                Marshal.Copy(buffer.Data, samples, 0, count);
                Level = Measure(samples, count); BuffersRead++;
                Check(waveInAddBuffer(handle, buffer.Header, HeaderSize));
            }
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                if (IsInput) waveInReset(handle); else waveOutReset(handle);
                foreach (var buffer in buffers)
                {
                    uint result = !buffer.Prepared ? 0 : IsInput ? waveInUnprepareHeader(handle, buffer.Header, HeaderSize) : waveOutUnprepareHeader(handle, buffer.Header, HeaderSize);
                    // Never free a buffer still owned by a faulty/disconnected driver.
                    if (result == 0) buffer.Free();
                }
                if (IsInput) waveInClose(handle); else waveOutClose(handle);
                handle = IntPtr.Zero;
            }
            else foreach (var buffer in buffers) buffer.Free();
            buffers.Clear(); Array.Clear(samples, 0, samples.Length); Level = 0;
        }

        [DllImport("winmm.dll")] private static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint waveOutGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint waveInMessage(IntPtr device, uint message, IntPtr p1, IntPtr p2);
        [DllImport("winmm.dll")] private static extern uint waveOutMessage(IntPtr device, uint message, IntPtr p1, IntPtr p2);
        [DllImport("winmm.dll")] private static extern uint waveInOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInStart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveInReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveInClose(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);
    }
}
