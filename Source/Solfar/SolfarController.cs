/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2021 - Steve G. Bjorg
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more
 * details.
 *
 * You should have received a copy of the GNU Affero General Public License along
 * with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RadiantPi.Cortex;
using RadiantPi.Kaleidescape;
using RadiantPi.Lumagen;
using RadiantPi.Lumagen.Model;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;
using TMDbLib.Client;

namespace Solfar {

    public class SolfarController : AController {

        //--- Fields ---
        private readonly IRadiancePro _radianceProClient;
        private readonly ISonyCledis _cledisClient;
        private readonly ITrinnovAltitude _trinnovClient;
        private readonly IKaleidescape _kaleidescapeClient;
        private readonly TMDbClient _movieDbClient;

        //--- Constructors ---
        public SolfarController(
            IRadiancePro radianceProClient,
            ISonyCledis cledisClient,
            ITrinnovAltitude altitudeClient,
            IKaleidescape kaleidescapeClient,
            TMDbClient movieDbClient,
            ILogger<SolfarController>? logger = null
        ) : base(logger) {
            _radianceProClient = radianceProClient ?? throw new ArgumentNullException(nameof(radianceProClient));
            _cledisClient = cledisClient ?? throw new ArgumentNullException(nameof(cledisClient));
            _trinnovClient = altitudeClient ?? throw new ArgumentNullException(nameof(altitudeClient));
            _kaleidescapeClient = kaleidescapeClient ?? throw new ArgumentNullException(nameof(kaleidescapeClient));
            _movieDbClient = movieDbClient ?? throw new ArgumentNullException(nameof(movieDbClient));
        }

        //--- Methods ---
        protected override async Task Initialize(CancellationToken cancellationToken) {

            // register all event listeners
            _radianceProClient.DisplayModeChanged += EventListener;
            _trinnovClient.AudioDecoderChanged += EventListener;
            _kaleidescapeClient.HighlightedSelectionChanged += EventListener;

            // initialize communication with devices
            await _trinnovClient.ConnectAsync().ConfigureAwait(false);
            await _kaleidescapeClient.ConnectAsync().ConfigureAwait(false);
        }

        protected override async Task Shutdown(CancellationToken cancellationToken) {

            // remove all event listeners
            _trinnovClient.AudioDecoderChanged -= EventListener;
            _radianceProClient.DisplayModeChanged -= EventListener;
            _kaleidescapeClient.HighlightedSelectionChanged -= EventListener;

            // close device connection
            _trinnovClient.Dispose();
            _kaleidescapeClient.Dispose();
            _radianceProClient.Dispose();
        }

        protected override async Task ProcessEventAsync(object? sender, EventArgs args, CancellationToken cancellationToken) {
            switch(args) {
            case DisplayModeChangedEventArgs displayModeChangedEventArgs:
                ProcessDisplayModeChange(displayModeChangedEventArgs.DisplayMode);
                break;
            case AudioDecoderChangedEventArgs audioDecoderChangedEventArgs:
                ProcessAudioCodeChange(audioDecoderChangedEventArgs);
                break;
            case HighlightedSelectionChangedEventArgs highlightedSelectionChangedEventArgs:
                ProcessHighlightedSelectionChange(highlightedSelectionChangedEventArgs);
                break;
            default:
                Logger?.LogWarning($"Unrecognized channel event: {args?.GetType().FullName}");
                break;
            }
        }

        private void ProcessDisplayModeChange(RadianceProDisplayMode radianceProDisplayMode) {

            // display conditions
            var fitHeight = LessThan(radianceProDisplayMode.DetectedAspectRatio, "178");
            var fitWidth = GreaterThanOrEqual(radianceProDisplayMode.DetectedAspectRatio, "178")
                && LessThanOrEqual(radianceProDisplayMode.DetectedAspectRatio, "200");
            var fitNative = GreaterThan(radianceProDisplayMode.DetectedAspectRatio, "200");
            var isHdr = radianceProDisplayMode.SourceDynamicRange == RadianceProDynamicRange.HDR;
            var is3D = (radianceProDisplayMode.Source3DMode != RadiancePro3D.Undefined) && (radianceProDisplayMode.Source3DMode != RadiancePro3D.Off);
            var isGameSource = radianceProDisplayMode.PhysicalInputSelected is 2 or 4 or 6 or 8;
            var isGui = radianceProDisplayMode.SourceVerticalRate == "050";

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
        }

        private void ProcessAudioCodeChange(AudioDecoderChangedEventArgs altitudeAudioDecoder)
            => OnValueChanged("Show Audio Codec", (Decoder: altitudeAudioDecoder.Decoder, Upmixer: altitudeAudioDecoder.Upmixer), async state => {

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

                    // clear menu in case it's shown
                    await _radianceProClient.SendAsync("!");

                    // show decoder infomration with optional upmixer details
                    var message = (upmixer.Length > 0)
                        ? $"{decoder} ({upmixer})"
                        : decoder;
                    await _radianceProClient.ShowMessageCenteredAsync(message, 2);
                }
            });

        private void ProcessHighlightedSelectionChange(HighlightedSelectionChangedEventArgs highlightedSelectionChangedEventArgs)
            => OnValueChanged("Show Kaleidescape Selection", highlightedSelectionChangedEventArgs.SelectionId, async selectionId => {
                var details = await _kaleidescapeClient.GetContentDetailsAsync(selectionId);

                // compose movies votes line from TheMovieDB
                string movieVotesLine = "";
                if(!string.IsNullOrEmpty(details.Title) && int.TryParse(details.Year, out var year)) {

                    // find movie on TheMovieD by title and year
                    var searchResults = await _movieDbClient.SearchMovieAsync(details.Title, year: year);
                    var firstResult = searchResults.Results.FirstOrDefault();
                    if(firstResult is not null) {

                        // show the movie score from TheMovieDB
                        movieVotesLine = $"TheMovieDB: {firstResult.VoteAverage:0.0} ({firstResult.VoteCount:N0} votes)";
                    }
                }

                // compose movie information line
                var movieInfoLine = string.IsNullOrEmpty(details.Rating)
                    ? $"{details.RunningTime} minutes"
                    : details.Rating.StartsWith("NR-", StringComparison.Ordinal)
                    ? $"{details.RunningTime} minutes [{details.Rating.Substring(3)}]"
                    : $"{details.RunningTime} minutes [{details.Rating}]";

                // clear menu in case it's shown
                await _radianceProClient.SendAsync("!");

                // show combined lines
                await _radianceProClient.ShowMessageCenteredAsync(movieVotesLine, movieInfoLine, 1);
            });
    }
}
