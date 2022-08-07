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

using System.Xml.Linq;
using System.Xml.XPath;

public sealed record MediaCenterPlaybackInfo(
    string ZoneID = "",
    string State = "",
    string FileKey = "",
    string NextFileKey = "",
    string PositionMS = "",
    string DurationMS = "",
    string ElapsedTimeDisplay = "",
    string RemainingTimeDisplay = "",
    string TotalTimeDisplay = "",
    string PositionDisplay = "",
    string PlayingNowPosition = "",
    string PlayingNowTracks = "",
    string PlayingNowPositionDisplay = "",
    string PlayingNowChangeCounter = "",
    string Bitrate = "",
    string Bitdepth = "",
    string SampleRate = "",
    string Channels = "",
    string Chapter = "",
    string Volume = "",
    string VolumeDisplay = "",
    string ImageURL = "",
    string Name = "",
    string Status = ""
);

public sealed class MediaCenterPlaybackInfoChangedEventArgs : EventArgs {

    //--- Constructors ---
    public MediaCenterPlaybackInfoChangedEventArgs(MediaCenterPlaybackInfo info) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
    }

    //--- Properties ---
    public MediaCenterPlaybackInfo Info { get; }
}

public sealed class MediaCenterClientConfig {

    //--- Properties ---
    public string? Url { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
}

// TODO: convert to generic client
public sealed class MediaCenterClient {

    //--- Fields ---
    private readonly MediaCenterClientConfig _config;
    private HttpClient _httpClient;
    private MediaCenterPlaybackInfo? _lastPlaybackInfo;
    private readonly CancellationTokenSource _loopCancellationTokenSource = new();

    //--- Constructors ---
    public MediaCenterClient(MediaCenterClientConfig config, HttpClient httpClient, ILogger<MediaCenterClient>? logger = null) {
        Logger = logger;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    //--- Events ---
    public event EventHandler<MediaCenterPlaybackInfoChangedEventArgs>? PlaybackInfoChanged;

    //--- Properties ---
    private ILogger? Logger { get; }

    //--- Methods ---
    public Task RunAsync() => Task.Run(async () => {
        while(!_loopCancellationTokenSource.IsCancellationRequested) {
            var playbackInfo = await GetPlaybackInfoAsync(_loopCancellationTokenSource.Token);
            if((playbackInfo is not null) && (playbackInfo != _lastPlaybackInfo)) {
                Logger?.LogInformation($"MediaCenter playback status changed: {playbackInfo}");
                _lastPlaybackInfo = playbackInfo;
                PlaybackInfoChanged?.Invoke(this, new(playbackInfo));
            }
            await Task.Delay(_config.Interval);
        }
    });

    public void Stop() => _loopCancellationTokenSource.Cancel();

    private async Task<MediaCenterPlaybackInfo?> GetPlaybackInfoAsync(CancellationToken cancellationToken = default) {
        try {
            var response = await _httpClient.GetAsync($"{_config.Url}/MCWS/v1/Playback/Info?Zone=-1", cancellationToken);
            var content = await response.Content.ReadAsStringAsync();
            Logger?.LogTrace($"Playback XML: {content}");
            var doc = XDocument.Parse(content, LoadOptions.None);
            return new MediaCenterPlaybackInfo(
                ZoneID: doc.XPathSelectElement("Response/Item[@Name='ZoneID']")?.Value ?? "",
                State: doc.XPathSelectElement("Response/Item[@Name='State']")?.Value ?? "",
                FileKey: doc.XPathSelectElement("Response/Item[@Name='FileKey']")?.Value ?? "",
                NextFileKey: doc.XPathSelectElement("Response/Item[@Name='NextFileKey']")?.Value ?? "",
                PositionMS: doc.XPathSelectElement("Response/Item[@Name='PositionMS']")?.Value ?? "",
                DurationMS: doc.XPathSelectElement("Response/Item[@Name='DurationMS']")?.Value ?? "",
                ElapsedTimeDisplay: doc.XPathSelectElement("Response/Item[@Name='ElapsedTimeDisplay']")?.Value ?? "",
                RemainingTimeDisplay: doc.XPathSelectElement("Response/Item[@Name='RemainingTimeDisplay']")?.Value ?? "",
                TotalTimeDisplay: doc.XPathSelectElement("Response/Item[@Name='TotalTimeDisplay']")?.Value ?? "",
                PositionDisplay: doc.XPathSelectElement("Response/Item[@Name='PositionDisplay']")?.Value ?? "",
                PlayingNowPosition: doc.XPathSelectElement("Response/Item[@Name='PlayingNowPosition']")?.Value ?? "",
                PlayingNowTracks: doc.XPathSelectElement("Response/Item[@Name='PlayingNowTracks']")?.Value ?? "",
                PlayingNowPositionDisplay: doc.XPathSelectElement("Response/Item[@Name='PlayingNowPositionDisplay']")?.Value ?? "",
                PlayingNowChangeCounter: doc.XPathSelectElement("Response/Item[@Name='PlayingNowChangeCounter']")?.Value ?? "",
                Bitrate: doc.XPathSelectElement("Response/Item[@Name='Bitrate']")?.Value ?? "",
                Bitdepth: doc.XPathSelectElement("Response/Item[@Name='Bitdepth']")?.Value ?? "",
                SampleRate: doc.XPathSelectElement("Response/Item[@Name='SampleRate']")?.Value ?? "",
                Channels: doc.XPathSelectElement("Response/Item[@Name='Channels']")?.Value ?? "",
                Chapter: doc.XPathSelectElement("Response/Item[@Name='Chapter']")?.Value ?? "",
                Volume: doc.XPathSelectElement("Response/Item[@Name='Volume']")?.Value ?? "",
                VolumeDisplay: doc.XPathSelectElement("Response/Item[@Name='VolumeDisplay']")?.Value ?? "",
                ImageURL: doc.XPathSelectElement("Response/Item[@Name='ImageURL']")?.Value ?? "",
                Name: doc.XPathSelectElement("Response/Item[@Name='Name']")?.Value ?? "",
                Status: doc.XPathSelectElement("Response/Item[@Name='Status']")?.Value ?? ""
            );
        } catch {
            return null;
        }
    }
}