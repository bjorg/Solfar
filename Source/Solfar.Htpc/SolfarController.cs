/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2022 - Steve G. Bjorg
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

namespace Solfar;

using System.Diagnostics;
using System.Runtime.Caching;
using Microsoft.Extensions.Logging;
using RadiantPi.Cortex;
using RadiantPi.Kaleidescape;
using RadiantPi.Lumagen;
using RadiantPi.Lumagen.Model;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;
using TMDbLib.Client;
using TMDbLib.Objects.Search;

public class SolfarController : AController {

    //--- Constants ---
    private const RadianceProStyle RadianceProStyleFitNative = RadianceProStyle.Style0;
    private const RadianceProStyle RadianceProStyleFitWidth = RadianceProStyle.Style1;
    private const RadianceProStyle RadianceProStyleFitHeight = RadianceProStyle.Style2;
    private const RadianceProMemory RadianceProMemoryFitNative = RadianceProMemory.MemoryA;
    private const RadianceProMemory RadianceProMemoryFitWidth = RadianceProMemory.MemoryB;
    private const RadianceProMemory RadianceProMemoryFitHeight = RadianceProMemory.MemoryC;

    //--- Fields ---
    private readonly IRadiancePro _radianceProClient;
    private readonly ISonyCledis _cledisClient;
    private readonly ITrinnovAltitude _trinnovClient;
    private readonly IKaleidescape _kaleidescapeClient;
    private readonly TMDbClient _movieDbClient;
    private readonly HttpClient _httpClient;
    private readonly MediaCenterClient _mediaCenterClient;
    private readonly MemoryCache _cache = new("TheMovieDB");
    private bool _sourceChanged;
    private RadianceProMemory? _selectMemory;

    //--- Constructors ---
    public SolfarController(
        IRadiancePro radianceProClient,
        ISonyCledis cledisClient,
        ITrinnovAltitude altitudeClient,
        IKaleidescape kaleidescapeClient,
        TMDbClient movieDbClient,
        HttpClient httpClient,
        MediaCenterClient mediaCenterClient,
        ILogger<SolfarController>? logger = null
    ) : base(logger) {
        _radianceProClient = radianceProClient ?? throw new ArgumentNullException(nameof(radianceProClient));
        _cledisClient = cledisClient ?? throw new ArgumentNullException(nameof(cledisClient));
        _trinnovClient = altitudeClient ?? throw new ArgumentNullException(nameof(altitudeClient));
        _kaleidescapeClient = kaleidescapeClient ?? throw new ArgumentNullException(nameof(kaleidescapeClient));
        _movieDbClient = movieDbClient ?? throw new ArgumentNullException(nameof(movieDbClient));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _mediaCenterClient = mediaCenterClient ?? throw new ArgumentNullException(nameof(mediaCenterClient));
    }

    //--- Methods ---
    protected override async Task Initialize(CancellationToken cancellationToken) {

        // register all event listeners
        _radianceProClient.DisplayModeChanged += EventListener;
        _trinnovClient.AudioDecoderChanged += EventListener;
        _kaleidescapeClient.HighlightedSelectionChanged += EventListener;
        _mediaCenterClient.PlaybackInfoChanged += EventListener;

        // initialize communication with devices
        await _trinnovClient.ConnectAsync().ConfigureAwait(false);
        await _kaleidescapeClient.ConnectAsync().ConfigureAwait(false);

        // fetch current RadiancePro settings (they are not automatically provided otherwise)
        await _radianceProClient.GetDisplayModeAsync().ConfigureAwait(false);

        // TODO: add periodic check for C-LED temperature and fan control
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
        case MediaCenterPlaybackInfoChangedEventArgs mediaCenterPlaybackInfoChangedEventArgs:
            ProcessMediaCenterPlaybackInfoChange(mediaCenterPlaybackInfoChangedEventArgs);
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
        var is3D = radianceProDisplayMode.Source3DMode is RadiancePro3D.FrameSequential or RadiancePro3D.FramePacked or RadiancePro3D.TopBottom or RadiancePro3D.SideBySide;
        var isGui = radianceProDisplayMode.SourceVerticalRate == "050";
        var isOppo = radianceProDisplayMode.PhysicalInputSelected is 1;
        var isAppleTv = radianceProDisplayMode.PhysicalInputSelected is 3;
        var isKaleidescape = radianceProDisplayMode.PhysicalInputSelected is 5;
        var isNVidiaTV = radianceProDisplayMode.PhysicalInputSelected is 7;
        var isHtpc2D = radianceProDisplayMode.PhysicalInputSelected is 2;
        var isHtpc3D = radianceProDisplayMode.PhysicalInputSelected is 4;

        // select video input
        TriggerOnTrue("Switch to Lumagen 2D", !isHtpc2D && !isHtpc3D && !is3D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.Hdmi1);
        });
        TriggerOnTrue("Switch to Lumagen 3D", !isHtpc2D && !isHtpc3D && is3D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.Hdmi2);
            await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode3);
        });
        TriggerOnTrue("Switch to HTPC 2D", isHtpc2D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.DisplayPortBoth);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await SwitchTo2DAsync();
        });
        TriggerOnTrue("Switch to HTPC 3D", isHtpc3D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.DisplayPortBoth);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await SwitchTo3DAsync();
        });

        // select audio input
        TriggerOnTrue("Switch to Lumagen Audio Output", !isHtpc2D && !isHtpc3D && !isKaleidescape && !isOppo, async () => {
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi7);
        });
        TriggerOnTrue("Switch to HTPC Audio Output", isHtpc2D || isHtpc3D, async () => {
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi6);
        });
        TriggerOnTrue("Switch to Kaleidescape Output", isKaleidescape, async () => {
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi5);
        });
        TriggerOnTrue("Switch to Oppo Output", isOppo, async () => {

            // TODO: switch between "Auto" and "Auro-3D"
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi4);
        });

        // select display brightness
        TriggerOnTrue("Switch to SDR", !isHtpc2D && !isHtpc3D && !is3D && !isHdr, async () => {
            await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode1);
        });
        TriggerOnTrue("Switch to HDR", !isHtpc2D && !isHtpc3D && !is3D && isHdr, async () => {
            await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode2);
        });

        // determine if the source has changed
        var sourceInput = radianceProDisplayMode.PhysicalInputSelected;
        var sourceDynamicRange = radianceProDisplayMode.SourceDynamicRange;
        var sourceVerticalRate = radianceProDisplayMode.SourceVerticalRate;
        var sourceVerticalResolution = radianceProDisplayMode.SourceVerticalResolution;
        TriggerOnValueChanged("Source Changed", $"{sourceInput}-{sourceDynamicRange}-{sourceVerticalRate}-{sourceVerticalResolution}", async (_) => {
            _sourceChanged = true;
            _selectMemory = RadianceProMemoryFitNative;
        });

        // select video processor aspect-ratio
        TriggerOnTrue("Fit GUI", !isHtpc2D && !isHtpc3D && !is3D && isGui, async () => {
            _selectMemory = RadianceProMemoryFitHeight;
        });
        TriggerOnTrue("Fit Height", !isHtpc2D && !isHtpc3D && !is3D && fitHeight, async () => {
            if(_sourceChanged || (RadianceProStyleFitHeight > radianceProDisplayMode.OutputStyle)) {
                _selectMemory = RadianceProMemoryFitHeight;
            }
        });
        TriggerOnTrue("Fit Width", !isHtpc2D && !isHtpc3D && !is3D && fitWidth && !isGui, async () => {
            if(_sourceChanged || (RadianceProStyleFitWidth > radianceProDisplayMode.OutputStyle)) {
                _selectMemory = RadianceProMemoryFitWidth;
            }
        });
        TriggerOnTrue("Fit Native", !isHtpc2D && !isHtpc3D && !is3D && fitNative && !isGui, async () => {
            if(_sourceChanged || (RadianceProStyleFitNative > radianceProDisplayMode.OutputStyle)) {
                _selectMemory = RadianceProMemoryFitNative;
            }
        });
        TriggerAlways("Apply Memory Change", async () => {
            if(_selectMemory is not null) {
                await _radianceProClient.SelectMemoryAsync(_selectMemory.Value);
            }
            _sourceChanged = false;
            _selectMemory = null;
        });
    }

    private void ProcessAudioCodeChange(AudioDecoderChangedEventArgs altitudeAudioDecoder)
        => TriggerOnValueChanged("Show Audio Codec", (Decoder: altitudeAudioDecoder.Decoder, Upmixer: altitudeAudioDecoder.Upmixer), async state => {

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

                // show decoder information with optional upmixer details
                var message = (upmixer.Length > 0)
                    ? $"{decoder} ({upmixer})"
                    : decoder;
                await _radianceProClient.ShowMessageCenteredAsync(message, "", 2);
            }
        });

    private void ProcessHighlightedSelectionChange(HighlightedSelectionChangedEventArgs highlightedSelectionChangedEventArgs)
        => TriggerOnValueChanged("Show Kaleidescape Selection", highlightedSelectionChangedEventArgs.SelectionId, async selectionId => {
            var details = await _kaleidescapeClient.GetContentDetailsAsync(selectionId);

            // "---------|---------|---------|"
            // "Score: 9.9 - 999 mins [PG-13] "
            // "---------|---------|---------|"

            // compose movies votes line from TheMovieDB
            string movieScore = "";
            if(!string.IsNullOrEmpty(details.Title) && int.TryParse(details.Year, out var year)) {

                // find movie on TheMovieDB by title and year, if it's not in the cache already
                var result = (SearchMovie?)_cache[selectionId];
                if(result is null) {

                    // TODO: add timeout to search request
                    var searchResults = await _movieDbClient.SearchMovieAsync(details.Title, year: year);

                    result = searchResults.Results.FirstOrDefault();
                    _cache.Add(selectionId, result ?? new SearchMovie(), DateTimeOffset.UtcNow.AddHours(24));
                }
                if(result?.Title is not null) {

                    // show the movie score from TheMovieDB
                    movieScore = $"Score: {result.VoteAverage:0.0} - ";
                }
            }

            // compose movie information line
            var movieInfo = string.IsNullOrEmpty(details.Rating)
                ? $"{details.RunningTime} mins"
                : $"{details.RunningTime} mins [{TrimNRPrefix(details.Rating)}]";

            // clear menu in case it's shown
            await _radianceProClient.SendAsync("!");

            // show combined lines
            await _radianceProClient.ShowMessageCenteredAsync(movieScore + movieInfo, "", 1);

            // local functions
            string TrimNRPrefix(string rating)
                => rating.StartsWith("NR-", StringComparison.Ordinal)
                    ? rating.Substring(3)
                    : rating;
        });

    private void ProcessMediaCenterPlaybackInfoChange(MediaCenterPlaybackInfoChangedEventArgs mediaCenterPlaybackInfoChangedEventArgs)
        => TriggerOnTrue("MediaCenter Playback Stopped", mediaCenterPlaybackInfoChangedEventArgs.Info.Status == "Stopped", async () => {

            // check if power is on
            var power = await _cledisClient.GetPowerStatusAsync();
            if(power != SonyCledisPowerStatus.On) {
                Logger?.LogInformation($"Sony Cledis controller is not turned on (expected: {SonyCledisPowerStatus.On}, found: {power})");
                return;
            }

            // check if the HTPC picture mode is selected
            var currentPictureMode = await _cledisClient.GetPictureModeAsync();
            if(currentPictureMode != SonyCledisPictureMode.Mode10) {
                Logger?.LogWarning($"Sony Cledis controller uses wrong picture mode for switching light output (expected: {SonyCledisPictureMode.Mode10}, current: {currentPictureMode})");
                return;
            }
            await _cledisClient.SetLightOutputMode(SonyCledisLightOutputMode.Low);
        });


    private async Task<string> SwitchTo2DAsync() {
        Logger?.LogInformation($"{nameof(SwitchTo2DAsync)} called");
        var response = "Ok";

        // check if Sony C-LED is turned on
        var power = await _cledisClient.GetPowerStatusAsync();
        if(power != SonyCledisPowerStatus.On) {
            response = $"Sony C-LED is not on (current: {power})";
            goto done;
        }

        // check if Dual-DisplayPort is the active input
        var input = await _cledisClient.GetInputAsync();
        if(input != SonyCledisInput.DisplayPortBoth) {
            response = $"Sony C-LED active input is not Dual-DisplayPort (current: {input})";
            goto done;
        }

        // check current display status
        var displayStatus = DisplayApi.GetCurrentDisplayStatus();
        switch(displayStatus.NVidiaMode) {
        default:
        case NVidiaMode.Undefined:
            response = $"HTPC NVidia display mode could not be identified (mode: {displayStatus.NVidiaMode})";
            break;
        case NVidiaMode.Disabled:
            response = $"HTPC NVidia adapter has not displays attached";
            break;
        case NVidiaMode.Surround3D:
            Logger?.LogInformation($"Disabling NVIDIA 3D surround mode");

            // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
            await SwitchNVidiaSurroundProfileAsync("configs/individual-3D.cfg");
            await Task.Delay(TimeSpan.FromSeconds(5));
            goto case NVidiaMode.Individual3DTiles;
        case NVidiaMode.Individual3DTiles:
            Logger?.LogInformation($"Disabling Sony C-LED 3D mode");

            // switch Sony C-LED to 2D mode
            await _cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Mode2D);
            await Task.Delay(TimeSpan.FromSeconds(15));
            goto case NVidiaMode.IndividualHdTiles;
        case NVidiaMode.IndividualHdTiles:
            Logger?.LogInformation($"Enabling NVIDIA 4K surround mode");

            // activate NVidia 4K surround mode
            await SwitchNVidiaSurroundProfileAsync("configs/surround-4K.cfg");
            await Task.Delay(TimeSpan.FromSeconds(5));
            goto case NVidiaMode.Surround4K;
        case NVidiaMode.Surround4K:

            // nothing to do
            break;
        }

        // check if audio needs to be enabled
        displayStatus = DisplayApi.GetCurrentDisplayStatus();
        if(!displayStatus.AudioEnabled) {
            Logger?.LogInformation($"Enabling 4K display profile");
            await SwitchMonitorProfileAsync("4K");
        }
    done:
        Logger?.LogInformation($"RESPONSE: {response}");
        return response;
    }

    private async Task<string> SwitchTo3DAsync() {
        Logger?.LogInformation($"{nameof(SwitchTo3DAsync)} called");
        var response = "Ok";

        // check if Sony C-LED is turned on
        var power = await _cledisClient.GetPowerStatusAsync();
        if(power != SonyCledisPowerStatus.On) {
            response = $"Sony C-LED is not on (current: {power})";
            goto done;
        }

        // check if Dual-DisplayPort is the active input
        var input = await _cledisClient.GetInputAsync();
        if(input != SonyCledisInput.DisplayPortBoth) {
            response = $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
            goto done;
        }

        // check current display status
        var displayStatus = DisplayApi.GetCurrentDisplayStatus();
        switch(displayStatus.NVidiaMode) {
        default:
        case NVidiaMode.Undefined:
            response = $"HTPC NVidia display mode could not be identified (mode: {displayStatus.NVidiaMode})";
            break;
        case NVidiaMode.Disabled:
            response = $"HTPC NVidia adapter has not displays attached";
            break;
        case NVidiaMode.Surround4K:
            Logger?.LogInformation($"Disabling NVIDIA 4K surround mode");

            // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
            await SwitchNVidiaSurroundProfileAsync("configs/individual-4xHD.cfg");
            await Task.Delay(TimeSpan.FromSeconds(5));
            goto case NVidiaMode.IndividualHdTiles;
        case NVidiaMode.IndividualHdTiles:
            Logger?.LogInformation($"Enabling Sony C-LED 3D mode");

            // switch Sony C-LED to 3D mode
            await _cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Mode3D);
            await Task.Delay(TimeSpan.FromSeconds(15));
            goto case NVidiaMode.Individual3DTiles;
        case NVidiaMode.Individual3DTiles:
            Logger?.LogInformation($"Enabling NVIDIA 3D surround mode");

            // activate NVidia 3D surround mode
            await SwitchNVidiaSurroundProfileAsync("configs/surround-3D.cfg");
            await Task.Delay(TimeSpan.FromSeconds(5));
            goto case NVidiaMode.Surround3D;
        case NVidiaMode.Surround3D:

            // nothing to do
            break;
        }

        // check if audio needs to be enabled
        displayStatus = DisplayApi.GetCurrentDisplayStatus();
        if(!displayStatus.AudioEnabled) {
            Logger?.LogInformation($"Enabling 3D display profile");
            await SwitchMonitorProfileAsync("3D");
        }
    done:
        Logger?.LogInformation($"RESPONSE: {response}");
        return response;
    }

    private async Task SwitchNVidiaSurroundProfileAsync(string profile) {
        Logger?.LogInformation($"Switch NVidia surround profile: {profile}");
        using var process = Process.Start(new ProcessStartInfo() {
            FileName = @"C:\Projects\NVIDIAInfo-v1.7.0\NVIDIAInfo.exe",
            Arguments = $"load \"{profile}\"",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        });
        if(process is not null) {
            await process.WaitForExitAsync();
            if(process.ExitCode != 0) {
                Logger?.LogWarning($"{nameof(SwitchNVidiaSurroundProfileAsync)}: exited with code: {process.ExitCode}");
            }
        } else {
            Logger?.LogError($"{nameof(SwitchNVidiaSurroundProfileAsync)}: unable to start process");
        }
    }

    private async Task SwitchMonitorProfileAsync(string profile) {
        Logger?.LogInformation($"Switch monitor profile: {profile}");
        using var process = Process.Start(new ProcessStartInfo() {
            FileName = @"C:\Program Files (x86)\DisplayFusion\DisplayFusionCommand.exe",
            Arguments = $"-monitorloadprofile \"{profile}\"",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        });
        if(process is not null) {
            await process.WaitForExitAsync();
            if(process.ExitCode != 0) {
                Logger?.LogWarning($"{nameof(SwitchMonitorProfileAsync)}: exited with code: {process.ExitCode}");
            }
        } else {
            Logger?.LogError($"{nameof(SwitchMonitorProfileAsync)}: unable to start process");
        }
    }
}
