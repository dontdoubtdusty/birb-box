using System.Numerics;
using ImGuiNET;

// The station finder: a window for putting Radio Browser stations on the radio's dial. Opened by clicking the
// radio's tuning window. An ImGui stand-in for now; its in-world home later is the tablet on the table.
//  - Find: search Radio Browser by name, genre or country (or paste a radio-browser.info link). Each result says
//    whether the radio can play it; Listen previews it through the radio, Add puts it on the dial.
//  - My dial: your stations (Tune, Remove) and your genres (how many stations each brings, Remove, add one).
// Every change goes on the dial at once and is saved to Radio/my-stations.json (see WebStations).
// Holds only what the window itself needs (the search box, the results); the stations live in WebStations.
public class StationFinder
{
    private static readonly string[] SearchByNames = { "Name", "Genre", "Country" };

    private readonly RadioPlayer player;
    public bool IsOpen { get; private set; }

    // FIND TAB
    private string query = "";
    private int searchBy = 0;
    private Task<List<WebStations.Found>>? searching;
    private List<WebStations.Found> results = new();
    private string searchNote = "Search for a station by name, genre or country. A radio-browser.info link works too.";

    // MY DIAL TAB
    private string newTag = "";

    public StationFinder(RadioPlayer player)
    {
        this.player = player;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else IsOpen = true;
    }

    // Closing ends any preview, so the radio goes back to the dial.
    public void Close()
    {
        IsOpen = false;
        player.StopPreview();
    }

    // ---------------- DRAW (ImGui; called between rlImGui.Begin and End) ----------------

    public void Draw()
    {
        if (!IsOpen) return;
        TakeSearchResult();

        Vector2 display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowSize(new Vector2(660, 480), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(display.X / 2f - 330, display.Y / 2f - 240), ImGuiCond.FirstUseEver);

        bool open = true;
        if (ImGui.Begin("Radio stations", ref open))
        {
            if (ImGui.BeginTabBar("radio tabs"))
            {
                if (ImGui.BeginTabItem("Find"))
                {
                    DrawFind();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("My dial"))
                {
                    DrawMyDial();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
        if (!open) Close();
    }

    // The search row, the results table, and the preview line under it.
    private void DrawFind()
    {
        ImGui.SetNextItemWidth(90);
        ImGui.Combo("##by", ref searchBy, SearchByNames, SearchByNames.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);
        bool enter = ImGui.InputTextWithHint("##query", "e.g. Radio Paradise, ambient, Iceland", ref query, 200, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        bool busy = searching != null;
        ImGui.BeginDisabled(busy || query.Trim().Length == 0);
        bool clicked = ImGui.Button(busy ? "Searching" : "Search", new Vector2(-1, 0));
        ImGui.EndDisabled();
        if ((enter || clicked) && !busy && query.Trim().Length > 0) StartSearch();

        Dim(searchNote);

        float tableHeight = ImGui.GetContentRegionAvail().Y - PreviewLineHeight();
        ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        if (results.Count > 0 && ImGui.BeginTable("results", 5, flags, new Vector2(0, tableHeight)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Station", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn("Genre", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("Country", ImGuiTableColumnFlags.WidthFixed, 56);
            ImGui.TableSetupColumn("Format", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableHeadersRow();

            for (int i = 0; i < results.Count; i++)
            {
                ImGui.PushID(i);
                DrawResultRow(results[i]);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        else if (results.Count == 0)
        {
            ImGui.Dummy(new Vector2(0, MathF.Max(0f, tableHeight)));
        }
        DrawPreviewLine();
    }

    // One search result. Stations the radio can't play are greyed out with the reason instead of buttons.
    private void DrawResultRow(WebStations.Found found)
    {
        WebStations.Info info = found.Info;
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        if (found.Playable) ImGui.TextUnformatted(info.Name);
        else Dim(info.Name);

        ImGui.TableNextColumn();
        Dim(ShortTags(found.Tags));

        ImGui.TableNextColumn();
        Dim(info.Country);

        ImGui.TableNextColumn();
        Dim(found.Bitrate > 0 ? $"{found.Codec} {found.Bitrate}k" : found.Codec);

        ImGui.TableNextColumn();
        if (!found.Playable)
        {
            Dim($"Can't play: {found.WhyNot}");
            return;
        }

        bool listening = player.IsPreviewing(info.Key);
        if (ImGui.SmallButton(listening ? "Stop" : "Listen"))
        {
            if (listening) player.StopPreview();
            else player.StartPreview(info);
        }
        ImGui.SameLine();
        if (player.Web.HasStation(info.Key))
        {
            Dim("On dial");
        }
        else if (ImGui.SmallButton("Add"))
        {
            player.Web.AddStation(info);
        }
    }

    // Your stations, then your genres.
    private void DrawMyDial()
    {
        WebStations web = player.Web;
        // The stations list is as tall as its rows, up to 45% of the tab; the genres scroll in what's left
        float usable = ImGui.GetContentRegionAvail().Y - PreviewLineHeight();
        float rowHeight = ImGui.GetTextLineHeightWithSpacing() + 2f;
        float listHeight = MathF.Min(web.MyStations.Count * rowHeight + 6f, usable * 0.45f);

        ImGui.TextUnformatted("Your stations");
        WebStations.MyStation? remove = null;
        ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        if (web.MyStations.Count == 0)
        {
            Dim("None yet. Find some on the Find tab.");
        }
        else if (ImGui.BeginTable("mine", 4, flags, new Vector2(0, listHeight)))
        {
            ImGui.TableSetupColumn("Station", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn("FM", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Genre", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 110);
            for (int i = 0; i < web.MyStations.Count; i++)
            {
                WebStations.MyStation station = web.MyStations[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(station.Name);
                ImGui.TableNextColumn();
                float mhz = player.FrequencyOf(station.Key);
                Dim(mhz > 0f ? $"{mhz:0.0}" : "-");
                ImGui.TableNextColumn();
                Dim(station.Genre);
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Tune")) player.TuneTo(station.Key);
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove")) remove = station;
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (remove != null) web.RemoveStation(remove);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Genres");
        Dim("Each genre brings its most-listened stations that the radio can play.");

        WebStations.TagPick? removeTag = null;
        float genresHeight = ImGui.GetContentRegionAvail().Y - PreviewLineHeight() - ImGui.GetFrameHeightWithSpacing();
        ImGui.BeginChild("genres", new Vector2(0, MathF.Max(genresHeight, ImGui.GetFrameHeightWithSpacing())));
        for (int i = 0; i < web.Tags.Count; i++)
        {
            WebStations.TagPick pick = web.Tags[i];
            ImGui.PushID(1000 + i);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(pick.Tag);
            ImGui.SameLine(160);
            int count = pick.Count;
            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt("stations", ref count)) web.SetTagCount(pick, count);
            ImGui.SameLine();
            int found = web.FoundForTag(pick.Tag);
            Dim(web.IsSearchingTag(pick.Tag) ? "looking up..." : found == 0 ? "none found yet" : $"{Math.Min(pick.Count, found)} on the dial");
            ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - 70);
            if (ImGui.SmallButton("Remove")) removeTag = pick;
            ImGui.PopID();
        }
        ImGui.EndChild();
        if (removeTag != null) web.RemoveTag(removeTag);

        ImGui.SetNextItemWidth(200);
        bool enter = ImGui.InputTextWithHint("##newtag", "add a genre, e.g. folk", ref newTag, 60, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Add genre") || enter) && newTag.Trim().Length > 0)
        {
            web.AddTag(newTag);
            newTag = "";
        }
        DrawPreviewLine();
    }

    // Under either tab: what's being previewed and how it's going, with a Stop button.
    private void DrawPreviewLine()
    {
        ImGui.Separator();
        string? name = player.PreviewName;
        if (name == null)
        {
            Dim("Listen to a station to hear it before adding it.");
            return;
        }
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Listening to {name}: {player.PreviewStatus()}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Stop##preview")) player.StopPreview();
    }

    private static float PreviewLineHeight() => ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2f;

    // ---------------- SEARCH ----------------

    private void StartSearch()
    {
        searchNote = "Searching Radio Browser...";
        searching = WebStations.Search(query, (WebStations.SearchBy)searchBy);
    }

    // Picks up a finished search: the results (playable ones first), or why it failed.
    private void TakeSearchResult()
    {
        if (searching == null || !searching.IsCompleted) return;
        if (searching.IsCompletedSuccessfully)
        {
            results = searching.Result.OrderBy(r => r.Playable ? 0 : 1).ToList();
            int playable = results.Count(r => r.Playable);
            searchNote = results.Count == 0
                ? "Nothing found. Try a shorter name, or search by genre."
                : $"{results.Count} found, {playable} the radio can play.";
        }
        else
        {
            results = new();
            searchNote = "Couldn't reach Radio Browser. Check the connection and try again.";
        }
        searching = null;
    }

    // ---------------- HELPERS ----------------

    // Grey text. TextUnformatted, since station names can hold a % that ImGui would read as formatting.
    private static void Dim(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    // The first two of a station's tags, for the Genre column.
    private static string ShortTags(string tags)
    {
        string[] parts = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(", ", parts.Take(2));
    }
}
