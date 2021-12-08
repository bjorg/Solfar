using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RadiantPi.Controller;
using RadiantPi.Lumagen;
using RadiantPi.Lumagen.Model;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;

namespace Solfar {

    public class SolfarController : AController {

        //--- Fields ---
        private IRadiancePro _radianceProClient;
        private ISonyCledis _cledisClient;
        protected ITrinnovAltitude _trinnovClient;
        private RadianceProDisplayMode _radianceProDisplayMode = new();
        private AudioDecoderChangedEventArgs _altitudeAudioDecoder = new("", "");

        //--- Constructors ---
        public SolfarController(
            IRadiancePro radianceProClient,
            ISonyCledis cledisClient,
            ITrinnovAltitude altitudeClient,
            ILogger<SolfarController>? logger = null
        ) : base(logger) {
            _radianceProClient = radianceProClient ?? throw new ArgumentNullException(nameof(radianceProClient));
            _cledisClient = cledisClient ?? throw new ArgumentNullException(nameof(cledisClient));
            _trinnovClient = altitudeClient ?? throw new ArgumentNullException(nameof(altitudeClient));
        }

        //--- Methods ---
        public override async Task Start() {
            await base.Start();
            _radianceProClient.DisplayModeChanged += EventListener;
            _trinnovClient.AudioDecoderChanged += EventListener;
            await _trinnovClient.ConnectAsync();
        }

        public override void Stop() {
            _trinnovClient.AudioDecoderChanged -= EventListener;
            _radianceProClient.DisplayModeChanged -= EventListener;
            base.Stop();
        }

        protected override bool ApplyEvent(object? sender, EventArgs args) {
            switch(args) {
            case DisplayModeChangedEventArgs displayModeChangedEventArgs:
                _radianceProDisplayMode = displayModeChangedEventArgs.DisplayMode;
                return true;
            case AudioDecoderChangedEventArgs audioDecoderChangedEventArgs:
                _altitudeAudioDecoder = audioDecoderChangedEventArgs;
                return true;
            default:
                Logger?.LogWarning($"unrecognized channel event: {args?.GetType().FullName}");
                return false;
            }
        }

        protected override void Evaluate() {

            // display conditions
            var fitHeight = LessThan(_radianceProDisplayMode.DetectedAspectRatio, "178");
            var fitWidth = GreaterThanOrEqual(_radianceProDisplayMode.DetectedAspectRatio, "178")
                && LessThanOrEqual(_radianceProDisplayMode.DetectedAspectRatio, "200");
            var fitNative = GreaterThan(_radianceProDisplayMode.DetectedAspectRatio, "200");
            var isHdr = _radianceProDisplayMode.SourceDynamicRange == RadianceProDynamicRange.HDR;
            var is3D = (_radianceProDisplayMode.Source3DMode != RadiancePro3D.Undefined) && (_radianceProDisplayMode.Source3DMode != RadiancePro3D.Off);
            var isGameSource = _radianceProDisplayMode.PhysicalInputSelected is 2 or 4 or 6 or 8;
            var isGui = _radianceProDisplayMode.SourceVerticalRate == "050";

            // display rules
            OnTrue("Switch to 2D", !is3D, async () => {
                await _cledisClient.SetInputAsync(SonyCledisInput.Hdmi1);
            });
            OnTrue("Switch to 3D", is3D, async () => {
                await _cledisClient.SetInputAsync(SonyCledisInput.Hdmi2);
                await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode3);
                await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryA);
            });
            OnTrue("Switch to SDR", !is3D && !isHdr, async () => {
                await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode1);
            });
            OnTrue("Switch to HDR", !is3D && isHdr, async () => {
                await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode2);
            });
            OnTrue("Fit Height", !is3D && !isGameSource && (fitHeight || isGui), async () => {
                await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryC);
            });
            OnTrue("Fit Width", !is3D && !isGameSource && fitWidth && !isGui, async () => {
                await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryB);
            });
            OnTrue("Fit Native", !is3D && !isGameSource && fitNative && !isGui, async () => {
                await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryA);
            });

            // audio rules
            OnValueChanged("Show Audio Codec", (Decoder: _altitudeAudioDecoder.Decoder, Upmixer: _altitudeAudioDecoder.Upmixer), async state => {

                // determine value for upmixer message
                var upmixer = state.Upmixer;
                switch(state.Upmixer) {
                case "none":
                    upmixer = "";
                    break;
                case "Neural:X":
                case "Dolby Surround":

                    // nothing to do
                    break;
                default:
                    Logger?.LogWarning($"Unrecognized upmixer: '{state.Upmixer}'");
                    break;
                }

                // determine value for decoder message
                var decoder = state.Decoder;
                switch(state.Decoder) {
                case "none":
                case "PCM":
                    decoder = "";
                    break;
                case "DD":
                    decoder = "Dolby";
                    break;
                case "DD+":
                    decoder = "Dolby+";
                    break;
                case "TrueHD":
                    decoder = "Dolby TrueHD";
                    break;
                case "ATMOS DD+":
                    decoder = "Dolby+ ATMOS";
                    break;
                case "ATMOS TrueHD":
                    decoder = "Dolby TrueHD ATMOS";
                    break;
                case "DTS":
                    decoder = "DTS";
                    break;
                case "DTS-HD MA":
                    decoder = "DTS-HD";
                    break;
                case "DTS-HD HI RES":
                    decoder = "DTS-HD HiRes";
                    break;
                case "DTS-HD MA Auro-3D":
                    decoder = "Auro-3D";
                    break;
                case "DTS:X MA":
                    decoder = "DTS:X";
                    break;
                default:
                    Logger?.LogWarning($"Unrecognized decoder: '{state.Decoder}'");
                    decoder = state.Decoder;
                    break;
                }

                // show message
                if(decoder != "") {
                    await _radianceProClient.SendAsync("!");
                    if(upmixer != "") {
                        await _radianceProClient.ShowMessageAsync($"{decoder} ({upmixer})", 2);
                    } else {
                        await _radianceProClient.ShowMessageAsync($"{decoder}", 2);
                    }
                }
            });
        }
    }
}
