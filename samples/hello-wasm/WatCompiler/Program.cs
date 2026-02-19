// Build a minimal WASM module by hand that imports estarter_log and estarter_api_call,
// defines linear memory, data segments for strings, and a _start function.

using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: WatCompiler <output.wasm>");
    return 1;
}
var wasmPath = args[0];

var b = new WasmBuilder();

// === Type section ===
// Type 0: (func (param i32 i32))                     -> estarter_log
// Type 1: (func (param i32 i32 i32 i32) (result i32)) -> estarter_api_call
// Type 2: (func)                                       -> _start
b.AddTypeSection([
    new FuncType([0x7F, 0x7F], []),
    new FuncType([0x7F, 0x7F, 0x7F, 0x7F], [0x7F]),
    new FuncType([], [])
]);

// === Import section ===
// func 0: env.estarter_log  (type 0)
// func 1: env.estarter_api_call (type 1)
b.AddImportSection([
    new Import("env", "estarter_log", 0x00, 0),
    new Import("env", "estarter_api_call", 0x00, 1)
]);

// === Function section ===
// func 2: _start (type 2)
b.AddFunctionSection([2]);

// === Memory section ===
// 1 page minimum, no max
b.AddMemorySection(1);

// === Export section ===
// export "memory" (memory 0)
// export "_start" (func 2)
b.AddExportSection([
    new Export("memory", 0x02, 0),
    new Export("_start", 0x00, 2)
]);

// === Data section ===
// Build string table
var strings = new Dictionary<string, (int offset, int length)>();
int dataOffset = 0;
var dataSegments = new List<(int offset, byte[] data)>();

void AddString(string key, string value)
{
    var bytes = Encoding.UTF8.GetBytes(value);
    // Align to 4 bytes
    var aligned = (dataOffset + 3) & ~3;
    strings[key] = (aligned, bytes.Length);
    dataSegments.Add((aligned, bytes));
    dataOffset = aligned + bytes.Length;
}

AddString("start_msg",    "[HelloWasm] Starting...");
AddString("ping_cmd",     "Ping");
AddString("ping_ok",      "[HelloWasm] Ping: OK");
AddString("ping_fail",    "[HelloWasm] Ping: FAIL");
AddString("time_cmd",     "GetTime");
AddString("time_ok",      "[HelloWasm] GetTime: OK");
AddString("time_fail",    "[HelloWasm] GetTime: FAIL");
AddString("sysinfo_cmd",  "GetSystemInfo");
AddString("sysinfo_ok",   "[HelloWasm] GetSystemInfo: OK");
AddString("sysinfo_fail", "[HelloWasm] GetSystemInfo: FAIL");
AddString("done_msg",     "[HelloWasm] Done. Exiting.");

// === Code section ===
// func 2 body (_start):
var code = new List<byte>();

// Helper: emit call $log(ptr, len)
void EmitLog(string key)
{
    var (off, len) = strings[key];
    EmitI32Const(code, off);
    EmitI32Const(code, len);
    code.Add(0x10); code.Add(0x00); // call $log (func index 0)
}

// Helper: emit call $api_call(cmdPtr, cmdLen, 0, 0) and leave result on stack
void EmitApiCall(string cmdKey)
{
    var (off, len) = strings[cmdKey];
    EmitI32Const(code, off);
    EmitI32Const(code, len);
    EmitI32Const(code, 0);
    EmitI32Const(code, 0);
    code.Add(0x10); code.Add(0x01); // call $api_call (func index 1)
}

// Log: Starting
EmitLog("start_msg");

// Ping
EmitApiCall("ping_cmd");
code.Add(0x45); // i32.eqz
code.Add(0x04); code.Add(0x40); // if void
EmitLog("ping_ok");
code.Add(0x05); // else
EmitLog("ping_fail");
code.Add(0x0B); // end if

// GetTime
EmitApiCall("time_cmd");
code.Add(0x45); // i32.eqz
code.Add(0x04); code.Add(0x40); // if void
EmitLog("time_ok");
code.Add(0x05); // else
EmitLog("time_fail");
code.Add(0x0B); // end if

// GetSystemInfo
EmitApiCall("sysinfo_cmd");
code.Add(0x45); // i32.eqz
code.Add(0x04); code.Add(0x40); // if void
EmitLog("sysinfo_ok");
code.Add(0x05); // else
EmitLog("sysinfo_fail");
code.Add(0x0B); // end if

// Log: Done
EmitLog("done_msg");

code.Add(0x0B); // end func

b.AddCodeSection([code.ToArray()]);

// Data section MUST come after Code section (0x0B > 0x0A).
b.AddDataSection(dataSegments);

// Write the final WASM binary
var wasm = b.Build();
File.WriteAllBytes(wasmPath, wasm);
Console.WriteLine($"Generated {wasmPath} ({wasm.Length} bytes)");
return 0;

// ── Helpers ──

static void EmitI32Const(List<byte> buf, int value)
{
    buf.Add(0x41); // i32.const
    EmitLeb128(buf, value);
}

static void EmitLeb128(List<byte> buf, int value)
{
    while (true)
    {
        byte b = (byte)(value & 0x7F);
        value >>= 7;
        if ((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0))
        {
            buf.Add(b);
            return;
        }
        buf.Add((byte)(b | 0x80));
    }
}

// ── WASM Builder types ──

record FuncType(byte[] Params, byte[] Results);
record Import(string Module, string Name, byte Kind, int TypeIndex);
record Export(string Name, byte Kind, int Index);

class WasmBuilder
{
    private readonly List<byte> _sections = new();

    public byte[] Build()
    {
        var result = new List<byte>();
        result.AddRange(new byte[] { 0x00, 0x61, 0x73, 0x6D }); // \0asm
        result.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 }); // version 1
        result.AddRange(_sections);
        return result.ToArray();
    }

    public void AddTypeSection(FuncType[] types)
    {
        var body = new List<byte>();
        EmitLeb128U(body, types.Length);
        foreach (var t in types)
        {
            body.Add(0x60); // func type
            EmitLeb128U(body, t.Params.Length);
            body.AddRange(t.Params);
            EmitLeb128U(body, t.Results.Length);
            body.AddRange(t.Results);
        }
        WriteSection(0x01, body);
    }

    public void AddImportSection(Import[] imports)
    {
        var body = new List<byte>();
        EmitLeb128U(body, imports.Length);
        foreach (var imp in imports)
        {
            EmitString(body, imp.Module);
            EmitString(body, imp.Name);
            body.Add(imp.Kind);
            EmitLeb128U(body, imp.TypeIndex);
        }
        WriteSection(0x02, body);
    }

    public void AddFunctionSection(int[] typeIndices)
    {
        var body = new List<byte>();
        EmitLeb128U(body, typeIndices.Length);
        foreach (var idx in typeIndices)
            EmitLeb128U(body, idx);
        WriteSection(0x03, body);
    }

    public void AddMemorySection(int minPages)
    {
        var body = new List<byte>();
        EmitLeb128U(body, 1); // 1 memory
        body.Add(0x00); // no max
        EmitLeb128U(body, minPages);
        WriteSection(0x05, body);
    }

    public void AddExportSection(Export[] exports)
    {
        var body = new List<byte>();
        EmitLeb128U(body, exports.Length);
        foreach (var exp in exports)
        {
            EmitString(body, exp.Name);
            body.Add(exp.Kind);
            EmitLeb128U(body, exp.Index);
        }
        WriteSection(0x07, body);
    }

    public void AddDataSection(List<(int offset, byte[] data)> segments)
    {
        var body = new List<byte>();
        EmitLeb128U(body, segments.Count);
        foreach (var (offset, data) in segments)
        {
            body.Add(0x00); // active, memory 0
            body.Add(0x41); // i32.const
            EmitLeb128(body, offset);
            body.Add(0x0B); // end
            EmitLeb128U(body, data.Length);
            body.AddRange(data);
        }
        WriteSection(0x0B, body);
    }

    public void AddCodeSection(byte[][] funcBodies)
    {
        var body = new List<byte>();
        EmitLeb128U(body, funcBodies.Length);
        foreach (var fb in funcBodies)
        {
            // Each function body = local count + code
            var funcBody = new List<byte>();
            EmitLeb128U(funcBody, 0); // 0 local declarations
            funcBody.AddRange(fb);
            EmitLeb128U(body, funcBody.Count);
            body.AddRange(funcBody);
        }
        WriteSection(0x0A, body);
    }

    private void WriteSection(byte id, List<byte> body)
    {
        _sections.Add(id);
        EmitLeb128U(_sections, body.Count);
        _sections.AddRange(body);
    }

    private static void EmitString(List<byte> buf, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        EmitLeb128U(buf, bytes.Length);
        buf.AddRange(bytes);
    }

    private static void EmitLeb128U(List<byte> buf, int value)
    {
        while (true)
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value == 0) { buf.Add(b); return; }
            buf.Add((byte)(b | 0x80));
        }
    }

    private static void EmitLeb128(List<byte> buf, int value)
    {
        while (true)
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if ((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0))
            { buf.Add(b); return; }
            buf.Add((byte)(b | 0x80));
        }
    }
}
