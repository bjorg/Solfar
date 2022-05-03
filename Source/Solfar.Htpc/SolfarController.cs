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
using Microsoft.Extensions.Logging;
using RadiantPi.Cortex;
using RadiantPi.Kaleidescape;
using RadiantPi.Lumagen;
using RadiantPi.Lumagen.Model;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;
using TMDbLib.Client;

public class SolfarController : AController {

    //--- Fields ---
    private readonly IRadiancePro _radianceProClient;
    private readonly ISonyCledis _cledisClient;
    private readonly ITrinnovAltitude _trinnovClient;
    private readonly IKaleidescape _kaleidescapeClient;
    private readonly TMDbClient _movieDbClient;
    private readonly HttpClient _httpClient;

    //--- Constructors ---
    public SolfarController(
        IRadiancePro radianceProClient,
        ISonyCledis cledisClient,
        ITrinnovAltitude altitudeClient,
        IKaleidescape kaleidescapeClient,
        TMDbClient movieDbClient,
        HttpClient httpClient,
        ILogger<SolfarController>? logger = null
    ) : base(logger) {
        _radianceProClient = radianceProClient ?? throw new ArgumentNullException(nameof(radianceProClient));
        _cledisClient = cledisClient ?? throw new ArgumentNullException(nameof(cledisClient));
        _trinnovClient = altitudeClient ?? throw new ArgumentNullException(nameof(altitudeClient));
        _kaleidescapeClient = kaleidescapeClient ?? throw new ArgumentNullException(nameof(kaleidescapeClient));
        _movieDbClient = movieDbClient ?? throw new ArgumentNullException(nameof(movieDbClient));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
        var isGui = radianceProDisplayMode.SourceVerticalRate == "050";
        var isOppo = radianceProDisplayMode.PhysicalInputSelected is 1;
        var isAppleTv = radianceProDisplayMode.PhysicalInputSelected is 3;
        var isKaleidescape = radianceProDisplayMode.PhysicalInputSelected is 5;
        var isNVidiaTV = radianceProDisplayMode.PhysicalInputSelected is 7;
        var isHtpc2D = radianceProDisplayMode.PhysicalInputSelected is 2;
        var isHtpc3D = radianceProDisplayMode.PhysicalInputSelected is 4;

        // select video input
        OnTrue("Switch to Lumagen 2D", !isHtpc2D && !isHtpc3D && !is3D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.Hdmi1);
        });
        OnTrue("Switch to Lumagen 3D", !isHtpc2D && !isHtpc3D && is3D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.Hdmi2);
            await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode3);
            await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryA);
        });
        OnTrue("Switch to HTPC 2D", isHtpc2D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.DisplayPortBoth);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _httpClient.PostAsync("http://192.168.0.236:5158/Go2D", content: null);
        });
        OnTrue("Switch to HTPC 3D", isHtpc3D, async () => {
            await _cledisClient.SetInputAsync(SonyCledisInput.DisplayPortBoth);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await SwitchTo3DAsync();
            await _httpClient.PostAsync("http://192.168.0.236:5158/Go3D", content: null);
        });

        // select audio input
        OnTrue("Switch to Lumagen Audio Output", !isHtpc2D && !isHtpc3D && !isKaleidescape && !isOppo, async () => {
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi7);
        });
        OnTrue("Switch to HTPC Audio Output", isHtpc2D || isHtpc3D, async () => {
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi6);
        });
        OnTrue("Switch to Kaleidescape Output", isKaleidescape, async () => {
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi5);
        });
        OnTrue("Switch to Oppo Output", isOppo, async () => {

            // TODO: switch between "Auto" and "Auro-3D"
            await _trinnovClient.SelectProfileAsync(TrinnovAltitudeProfile.Hdmi4);
        });

        // select display brightness
        OnTrue("Switch to SDR", !isHtpc2D && !isHtpc3D && !is3D && !isHdr, async () => {
            await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode1);
        });
        OnTrue("Switch to HDR", !isHtpc2D && !isHtpc3D && !is3D && isHdr, async () => {
            await _cledisClient.SetPictureModeAsync(SonyCledisPictureMode.Mode2);
        });

        // select video processor aspect-ratio
        OnTrue("Fit Height", !isHtpc2D && !isHtpc3D && !is3D && (fitHeight || isGui), async () => {
            await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryC);
        });
        OnTrue("Fit Width", !isHtpc2D && !isHtpc3D && !is3D && fitWidth && !isGui, async () => {
            await _radianceProClient.SelectMemoryAsync(RadianceProMemory.MemoryB);
        });
        OnTrue("Fit Native", !isHtpc2D && !isHtpc3D && !is3D && fitNative && !isGui, async () => {
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

                // show decoder information with optional upmixer details
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

                // find movie on TheMovieDB by title and year

                // TODO: add timeout
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
