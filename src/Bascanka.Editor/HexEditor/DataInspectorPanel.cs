using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Bascanka.Editor.Themes;

namespace Bascanka.Editor.HexEditor;

/// <summary>
/// A panel that inspects the selected byte(s) in the hex editor and displays
/// their interpretation as various data types: integers, floats, binary,
/// character encodings, and hex strings.
/// Uses a 4-column layout (Type, Value, Type, Value) to reduce vertical space.
/// </summary>
public sealed class DataInspectorPanel : UserControl
{
    // ── Fields ──────────────────────────────────────────────────────────

    private readonly ListView _listView;
    private readonly CheckBox _bigEndianToggle;
    private bool _bigEndian;
    private byte[] _data = [];
    private long _offset;
    private long _length;
    private ITheme _theme;

    // ── Construction ────────────────────────────────────────────────────

    public DataInspectorPanel()
    {
        _theme = new DarkTheme();

        _bigEndianToggle = new CheckBox
        {
            Text = "Big-endian",
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = _theme.EditorForeground,
            BackColor = _theme.EditorBackground,
            Padding = new Padding(4, 2, 0, 0),
        };
        _bigEndianToggle.CheckedChanged += (_, _) =>
        {
            _bigEndian = _bigEndianToggle.Checked;
            UpdateInspector();
        };

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.EditorForeground,
        };
        _listView.Columns.Add("Type", 90);
        _listView.Columns.Add("Value", 120);
        _listView.Columns.Add("Type", 90);
        _listView.Columns.Add("Value", 120);

        Controls.Add(_listView);
        Controls.Add(_bigEndianToggle);

        Resize += (_, _) => AdjustColumnWidths();

        ApplyTheme();
    }

    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>The current theme.</summary>
    public ITheme Theme
    {
        get => _theme;
        set
        {
            _theme = value ?? new DarkTheme();
            ApplyTheme();
        }
    }

    /// <summary>Whether to interpret multi-byte values as big-endian.</summary>
    public bool BigEndian
    {
        get => _bigEndian;
        set
        {
            _bigEndian = value;
            _bigEndianToggle.Checked = value;
            UpdateInspector();
        }
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// Updates the inspector with the currently selected data.
    /// </summary>
    /// <param name="data">The full data buffer.</param>
    /// <param name="offset">Selected byte offset.</param>
    /// <param name="length">Selection length (at least 1).</param>
    public void Inspect(byte[] data, long offset, long length)
    {
        _data = data ?? [];
        _offset = offset;
        _length = Math.Max(1, length);
        UpdateInspector();
    }

    // ── Inspector logic ─────────────────────────────────────────────────

    private void UpdateInspector()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        if (_data.Length == 0 || _offset < 0 || _offset >= _data.Length)
        {
            _listView.EndUpdate();
            return;
        }

        // Collect all key-value pairs first.
        var entries = new List<(string Type, string Value)>();

        long available = _data.Length - _offset;

        // --- Single byte ---
        byte b = _data[_offset];
        entries.Add(("Int8 (signed)", ((sbyte)b).ToString()));
        entries.Add(("UInt8", b.ToString()));

        // --- Binary ---
        entries.Add(("Binary", Convert.ToString(b, 2).PadLeft(8, '0')));

        // --- Int16 ---
        if (available >= 2)
        {
            short i16 = ReadInt16(_offset);
            ushort u16 = (ushort)i16;
            string endianLabel = _bigEndian ? "BE" : "LE";
            entries.Add(($"Int16 {endianLabel}", i16.ToString()));
            entries.Add(($"UInt16 {endianLabel}", u16.ToString()));
        }

        // --- Int32 ---
        if (available >= 4)
        {
            int i32 = ReadInt32(_offset);
            uint u32 = (uint)i32;
            string endianLabel = _bigEndian ? "BE" : "LE";
            entries.Add(($"Int32 {endianLabel}", i32.ToString()));
            entries.Add(($"UInt32 {endianLabel}", u32.ToString()));
        }

        // --- Int64 ---
        if (available >= 8)
        {
            long i64 = ReadInt64(_offset);
            ulong u64 = (ulong)i64;
            string endianLabel = _bigEndian ? "BE" : "LE";
            entries.Add(($"Int64 {endianLabel}", i64.ToString()));
            entries.Add(($"UInt64 {endianLabel}", u64.ToString()));
        }

        // --- Float (IEEE 754) ---
        if (available >= 4)
        {
            int raw = ReadInt32(_offset);
            float f = BitConverter.Int32BitsToSingle(raw);
            entries.Add(("Float", f.ToString("G9")));
        }

        // --- Double (IEEE 754) ---
        if (available >= 8)
        {
            long raw = ReadInt64(_offset);
            double d = BitConverter.Int64BitsToDouble(raw);
            entries.Add(("Double", d.ToString("G17")));
        }

        // --- UTF-8 char ---
        try
        {
            int bytesForChar = GetUtf8CharLength(b);
            if (bytesForChar > 0 && _offset + bytesForChar <= _data.Length)
            {
                byte[] charBytes = new byte[bytesForChar];
                Array.Copy(_data, _offset, charBytes, 0, bytesForChar);
                string utf8Char = Encoding.UTF8.GetString(charBytes);
                entries.Add(("UTF-8 char", utf8Char.Length > 0 ? $"'{utf8Char}' (U+{char.ConvertToUtf32(utf8Char, 0):X4})" : "(invalid)"));
            }
        }
        catch
        {
            entries.Add(("UTF-8 char", "(invalid)"));
        }

        // --- UTF-16 char ---
        if (available >= 2)
        {
            ushort u16Val = (ushort)ReadInt16(_offset);
            char c16 = (char)u16Val;
            string display = char.IsControl(c16) ? $"U+{u16Val:X4}" : $"'{c16}' (U+{u16Val:X4})";
            entries.Add(("UTF-16 char", display));
        }

        // --- Hex string ---
        long hexLen = Math.Min(_length > 1 ? _length : 1, Math.Min(available, 16));
        StringBuilder hexSb = new();
        for (long i = 0; i < hexLen; i++)
        {
            if (i > 0) hexSb.Append(' ');
            hexSb.Append(_data[_offset + i].ToString("X2"));
        }
        if (_length > 16) hexSb.Append("...");
        entries.Add(("Hex", hexSb.ToString()));

        // Fill ListView rows: 2 entries per row (4 columns).
        for (int i = 0; i < entries.Count; i += 2)
        {
            var item = new ListViewItem(entries[i].Type) { UseItemStyleForSubItems = true };
            item.SubItems.Add(entries[i].Value);

            if (i + 1 < entries.Count)
            {
                item.SubItems.Add(entries[i + 1].Type);
                item.SubItems.Add(entries[i + 1].Value);
            }
            else
            {
                item.SubItems.Add(string.Empty);
                item.SubItems.Add(string.Empty);
            }

            _listView.Items.Add(item);
        }

        _listView.EndUpdate();
    }

    // ── Byte-order-aware reading ────────────────────────────────────────

    private short ReadInt16(long offset)
    {
        byte b0 = _data[offset];
        byte b1 = _data[offset + 1];
        if (_bigEndian)
            return (short)((b0 << 8) | b1);
        return (short)((b1 << 8) | b0);
    }

    private int ReadInt32(long offset)
    {
        Span<byte> buf = stackalloc byte[4];
        for (int i = 0; i < 4; i++)
            buf[i] = _data[offset + i];
        if (_bigEndian)
            buf.Reverse();
        return BitConverter.ToInt32(buf);
    }

    private long ReadInt64(long offset)
    {
        Span<byte> buf = stackalloc byte[8];
        for (int i = 0; i < 8; i++)
            buf[i] = _data[offset + i];
        if (_bigEndian)
            buf.Reverse();
        return BitConverter.ToInt64(buf);
    }

    /// <summary>
    /// Returns the expected byte length of a UTF-8 character given its lead byte,
    /// or 0 if the byte is a continuation or invalid lead byte.
    /// </summary>
    private static int GetUtf8CharLength(byte leadByte) => leadByte switch
    {
        < 0x80 => 1,
        < 0xC0 => 0,  // continuation byte
        < 0xE0 => 2,
        < 0xF0 => 3,
        < 0xF8 => 4,
        _ => 0,
    };

    // ── Layout ──────────────────────────────────────────────────────────

    private void AdjustColumnWidths()
    {
        if (_listView.Columns.Count < 4) return;
        int totalWidth = _listView.ClientSize.Width;
        int typeWidth = (int)(totalWidth * 0.2);
        int valueWidth = (totalWidth / 2) - typeWidth;
        _listView.Columns[0].Width = typeWidth;
        _listView.Columns[1].Width = valueWidth;
        _listView.Columns[2].Width = typeWidth;
        _listView.Columns[3].Width = valueWidth;
    }

    // ── Theming ─────────────────────────────────────────────────────────

    private void ApplyTheme()
    {
        BackColor = _theme.EditorBackground;
        ForeColor = _theme.EditorForeground;

        _listView.BackColor = _theme.EditorBackground;
        _listView.ForeColor = _theme.EditorForeground;

        _bigEndianToggle.BackColor = _theme.EditorBackground;
        _bigEndianToggle.ForeColor = _theme.EditorForeground;

        Invalidate(true);
    }
}
