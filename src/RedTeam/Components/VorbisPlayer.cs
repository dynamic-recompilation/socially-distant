﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using NVorbis;

namespace RedTeam.Components
{
    public class VorbisPlayer : SceneComponent
    {
        // thank you declan hoare
        private const double sc16 = 0x7FFF + 0.4999999999999999;

        private byte[] _mgBuffer = Array.Empty<byte>();
        private int _channels;
        private int _sampleRate;
        private float[] _buffer;
        private short[] _intermediate = Array.Empty<short>();
        private TimeSpan _totalTime;
        private TimeSpan _playTime;
        private bool _isPlaying = false;
        private VorbisReader _reader;
        private DynamicSoundEffectInstance _output;
        private bool _isLooped = false;
        private ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();
        private Task _decoder;
        private ConcurrentQueue<float> _powers = new ConcurrentQueue<float>();
        private float _power;
        private float _lowest;
        private bool _endOfPlay = false;
        
        
        public bool IsPlaying => _isPlaying;
        private float _sensitivity = 6;

        public float Power => (Math.Abs(_lowest) - Math.Abs(_power)) * _sensitivity;
        
        private void Cleanup()
        {
            // this hell stops the audio output and makes sure it's cleaned up.
            if (_output != null)
            {
                if (!_output.IsDisposed)
                {
                    if (_output.State != SoundState.Stopped)
                    {
                        _output.Stop();
                    }

                    _output.Dispose();
                }

                _output = null;
            }

            // this kills the vorbis stream
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }

            _buffer = null;
        }

        public void OpenResource(Assembly asm, string resourceName)
        {
            // stop the current stream
            Cleanup();

            // try to open a resource.
            var stream = asm.GetManifestResourceStream(resourceName);

            // null-check.
            if (stream == null)
                throw new InvalidOperationException($"Could not open resource '{resourceName}' from {asm.FullName}");

            // open the stream as a vorbis resource.
            _reader = new NVorbis.VorbisReader(stream, false);

            // timespans
            _totalTime = _reader.TotalTime;
            _playTime = _reader.DecodedTime;

            // Allocate the sample buffer.
            _channels = _reader.Channels;
            _sampleRate = _reader.SampleRate;
            _buffer = new float[(_channels * _sampleRate) / 60];

            // Allocate the output sound.
            _output = new DynamicSoundEffectInstance(_sampleRate, AudioChannels.Stereo);

            // play.
            _isPlaying = true;
            _output.Play();
            _endOfPlay = false;
        }

        private void WriteSamples()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
            {
                foreach (var sample in _intermediate)
                    writer.Write(sample);
            }
            _mgBuffer = ms.ToArray();
        }
        
        protected override void OnUpdate(GameTime gameTime)
        {
            if (_isPlaying)
            {
                if (_output.PendingBufferCount < 2 && !_endOfPlay)
                {
                    var count = _reader.ReadSamples(_buffer, 0, _buffer.Length);

                    if (count > 0)
                    {
                        if (_intermediate.Length != count)
                            Array.Resize(ref _intermediate, count);

                        var dB = 0f;
                        var lowest = 0f;
                        
                        for (var i = 0; i < count; i++)
                        {
                            var sample = _buffer[i];
                            var pcm = (short) (sample * sc16);
                            _intermediate[i] = pcm;

                            var max = (float) short.MaxValue;
                            var p = (float) pcm / max;
                            dB = MathF.Log(MathF.Abs(p));

                            if (dB < lowest)
                                lowest = dB;
                        }

                        _power = dB;
                        _lowest = lowest;
                        
                        WriteSamples();

                        _output.SubmitBuffer(_mgBuffer);
                    }
                    else
                    {
                        _endOfPlay = true;
                    }
                }

                if (_endOfPlay && _output.PendingBufferCount <= 0)
                {
                    if (_isLooped)
                    {
                        _reader.DecodedPosition = 0;
                        _endOfPlay = false;
                    }
                    else
                    {
                        _isPlaying = false;
                        Cleanup();
                    }
                }
            }
        }
    }
}