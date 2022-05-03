namespace Solfar.HtpcServer;

using MediaCenter;

//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_VOLUME_CHANGED, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_VOLUME_CHANGED, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_PLAYLIST_FILES_CHANGED, 553159380)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_PLAYERSTATE_CHANGE, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_VOLUME_CHANGED, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_TRACK_CHANGE, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_PLAYERSTATE_CHANGE, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_PLAYERSTATE_CHANGE, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_VOLUME_CHANGED, 0)
//EVENT: MJEvent type: MCCommand(MCC: NOTIFY_TRACK_CHANGE, 0)

public partial class Form1 : Form {

    //--- Fields ---
    private IMJAutomation? _mediaCenterClient;

    //--- Constructors ---
    public Form1() => InitializeComponent();

    //--- Properties ---
    public IMJAutomation MediaCenterClient => _mediaCenterClient ?? throw new InvalidOperationException();
    public IMJPlaybackAutomation PlaybackAutomation => (IMJPlaybackAutomation)MediaCenterClient;

    //--- Methods ---
    private void button1_Click(object sender, EventArgs e) {
        if(_mediaCenterClient is null) {
            _mediaCenterClient = new MCAutomationClass();
            if(_mediaCenterClient is null) {
                textBox1.Text = "Nope";
                return;
            }
            textBox1.Text = "Success!";
            var events = (IMJAutomationEvents_Event)_mediaCenterClient;
            events.FireMJEvent += Events_FireMJEvent;
            button1.Enabled = false;
        }
    }

    private void Events_FireMJEvent(string bstrType, string bstrParam1, string bstrParam2) {
        Invoke(() => ShowEvent(bstrType, bstrParam1, bstrParam2));
    }

    private void ShowEvent(string bstrType, string bstrParam1, string bstrParam2) {
        textBox1.Text = $"{bstrType}({bstrParam1}, {bstrParam2})";

        if(bstrType != "MJEvent type: MCCommand") {
            return;
        }
Console.WriteLine($"EVENT: {textBox1.Text}");
        switch(bstrParam1) {
        case "MCC: NOTIFY_PLAYERSTATE_CHANGE":
            var playlist = _mediaCenterClient.GetCurPlaylist();
            if(playlist is not null) {
                var file = playlist.GetFile(playlist.Position);
                foreach(var property in file.GetType().GetProperties(System.Reflection.BindingFlags.Public)) {
                    Console.WriteLine($"Property: {property.Name}: {property.GetValue(file)}");
                }
            }
            break;
        }
    }
}
