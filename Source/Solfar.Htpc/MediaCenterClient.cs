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
    public TimeSpan Delay { get; set; }
}

public sealed class MediaCenterClient {

    //--- Fields ---
    private readonly MediaCenterClientConfig _config;
    private HttpClient _httpClient;
    private ILogger? _logger;
    private MediaCenterPlaybackInfo? _lastPlaybackInfo;
    private readonly CancellationTokenSource _loopCancellationTokenSource = new();

    //--- Constructors ---
    public MediaCenterClient(MediaCenterClientConfig config, HttpClient httpClient, ILogger<MediaCenterClient>? logger = null) {
        _logger = logger;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    //--- Events ---
    public event EventHandler<MediaCenterPlaybackInfoChangedEventArgs>? PlaybackInfoChanged;

    //--- Methods ---
    public Task RunAsync() => Task.Run(async () => {
        while(!_loopCancellationTokenSource.IsCancellationRequested) {
            await Task.Delay(_config.Delay);
            var playbackInfo = await GetPlaybackInfoAsync();
            if(playbackInfo != _lastPlaybackInfo) {
                _lastPlaybackInfo = playbackInfo;
                PlaybackInfoChanged?.Invoke(this, new(playbackInfo));
            }
        }
    });

    public void Stop() => _loopCancellationTokenSource.Cancel();

    private async Task<MediaCenterPlaybackInfo> GetPlaybackInfoAsync() {
        var response = await _httpClient.GetAsync($"{_config.Url}/MCWS/v1/Playback/Info?Zone=-1");
        var doc = XDocument.Load(response.Content.ReadAsStream(), LoadOptions.None);
        return new MediaCenterPlaybackInfo(
            ZoneID: (string?)doc.XPathSelectElement("Response/Item[Name='ZoneID']") ?? "",
            State: (string?)doc.XPathSelectElement("Response/Item[Name='State']") ?? "",
            FileKey: (string?)doc.XPathSelectElement("Response/Item[Name='FileKey']") ?? "",
            NextFileKey: (string?)doc.XPathSelectElement("Response/Item[Name='NextFileKey']") ?? "",
            PositionMS: (string?)doc.XPathSelectElement("Response/Item[Name='PositionMS']") ?? "",
            DurationMS: (string?)doc.XPathSelectElement("Response/Item[Name='DurationMS']") ?? "",
            ElapsedTimeDisplay: (string?)doc.XPathSelectElement("Response/Item[Name='ElapsedTimeDisplay']") ?? "",
            RemainingTimeDisplay: (string?)doc.XPathSelectElement("Response/Item[Name='RemainingTimeDisplay']") ?? "",
            TotalTimeDisplay: (string?)doc.XPathSelectElement("Response/Item[Name='TotalTimeDisplay']") ?? "",
            PositionDisplay: (string?)doc.XPathSelectElement("Response/Item[Name='PositionDisplay']") ?? "",
            PlayingNowPosition: (string?)doc.XPathSelectElement("Response/Item[Name='PlayingNowPosition']") ?? "",
            PlayingNowTracks: (string?)doc.XPathSelectElement("Response/Item[Name='PlayingNowTracks']") ?? "",
            PlayingNowPositionDisplay: (string?)doc.XPathSelectElement("Response/Item[Name='PlayingNowPositionDisplay']") ?? "",
            PlayingNowChangeCounter: (string?)doc.XPathSelectElement("Response/Item[Name='PlayingNowChangeCounter']") ?? "",
            Bitrate: (string?)doc.XPathSelectElement("Response/Item[Name='Bitrate']") ?? "",
            Bitdepth: (string?)doc.XPathSelectElement("Response/Item[Name='Bitdepth']") ?? "",
            SampleRate: (string?)doc.XPathSelectElement("Response/Item[Name='SampleRate']") ?? "",
            Channels: (string?)doc.XPathSelectElement("Response/Item[Name='Channels']") ?? "",
            Chapter: (string?)doc.XPathSelectElement("Response/Item[Name='Chapter']") ?? "",
            Volume: (string?)doc.XPathSelectElement("Response/Item[Name='Volume']") ?? "",
            VolumeDisplay: (string?)doc.XPathSelectElement("Response/Item[Name='VolumeDisplay']") ?? "",
            ImageURL: (string?)doc.XPathSelectElement("Response/Item[Name='ImageURL']") ?? "",
            Name: (string?)doc.XPathSelectElement("Response/Item[Name='Name']") ?? "",
            Status: (string?)doc.XPathSelectElement("Response/Item[Name='Status']") ?? ""
        );
    }
}