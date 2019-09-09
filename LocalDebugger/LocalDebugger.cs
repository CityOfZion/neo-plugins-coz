using System;
using System.Text;
using static System.Linq.Enumerable;
using System.Collections.Generic;
using System.Threading;
using Neo.VM;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Network.P2P.Payloads;
using Neo.Ledger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins
{
    public class LocalDebugger : Plugin, IPersistencePlugin
    {
        public bool DebuggerActive { get; internal protected set; }
        private Debugger _debugger;
        private ApplicationEngine _engine;
        private uint _height;
        private bool _continue;
        private string _lastCmd;
        private uint _lineOffset;
        private Dictionary<UInt160, HashSet<uint>> _script_break_points;
        private HashSet<UInt256> _tx_break_points;
        private HashSet<uint> _block_break_points;
        private List<string> commandList;
        private string prevScript;

        public override void Configure()
        {
            _script_break_points = new Dictionary<UInt160, HashSet<uint>>();
            _tx_break_points = new HashSet<UInt256>();
            _block_break_points = new HashSet<uint>();

            commandList = new List<string>() {
                "debug {on|off} - enable debugger",
                "bp {script hash} {offset} - set a breakpoint on a smart contract",
                "tbp {tx hash} - set a breakpoint on a transaction",
                "bbp {height} - set a breakpoint on a block",
                "br {script hash} {offset} - remove a breakpoint on a smart contract",
                "tbr {tx hash} - remove a breakpoint on a transaction",
                "bbr {height} - remove a breakpoint on a block",
                "bl - list all breakpoints",
                "si - step into",
                "s - step over",
                "so - step out",
                "c - continue",
                "estack - examine the EvaluationStack",
                "astack - examine the AltStack",
                "dis {offset} - disassemble the currently running contract",
            };

            Settings.Load(GetConfiguration());

            DebuggerActive = Settings.Default.ActivateOnStart;

            foreach (uint block in Settings.Default.PreloadBlockBreakPoints)
                AddBlockBreakPoint(new string[] { "bbp", block.ToString() });

            foreach (string tx in Settings.Default.PreloadTxBreakPoints)
                AddTxBreakPoint(new string[] { "tbp", tx });

            foreach (KeyValuePair<string, uint> s in Settings.Default.PreloadScriptBreakPoints)
                AddBreakPoint(new string[] { "bp", s.Key, s.Value.ToString() });
        }


        public void OnCommit(Snapshot snapshot)
        {
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return false;
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            _height = snapshot.Height;
            if (DebuggerActive)
            {
                foreach (Blockchain.ApplicationExecuted a in applicationExecutedList)
                {
                    ApplicationExecutionResult r = a.ExecutionResults[0];
                    InvocationTransaction tx = (InvocationTransaction)a.Transaction;
                    using (Snapshot newsnapshot = Blockchain.Singleton.GetSnapshot())
                    {
                        using (_engine = new ApplicationEngine(TriggerType.Application, tx, newsnapshot, tx.Gas, false))
                        {
                            _engine.LoadScript(tx.Script);
                            _debugger = new Debugger(_engine);
                            _lineOffset = 0;

                            Execute(r);

                        }
                    }
                }
            }
        }

        public bool Execute(ApplicationExecutionResult r)
        {
            bool reportResults = false;
            try
            {
                _continue = true;
                while (true)
                {
                    if (DebuggerActive)
                    {
                        InvocationTransaction tx = (InvocationTransaction)_engine.ScriptContainer;
                        // Script:offset breakpoint
                        UInt160 scripthash = new UInt160(_engine.CurrentContext.ScriptHash);
                        uint pos = (uint) _engine.CurrentContext.InstructionPointer;
                        if (_continue && _script_break_points.TryGetValue(scripthash, out HashSet<uint> hashset) && hashset.Contains(pos))
                        {
                            Console.WriteLine($"Breakpoint hit at script {scripthash.ToString()}:{pos}");
                            Console.WriteLine($"Block: {_height}");
                            Console.WriteLine($"Tx: {tx.Hash.ToString()}");
                            PrintNext();
                            _continue = false;
                            reportResults = true;
                        }
                        if (_engine.CurrentContext.InstructionPointer == 0)
                        {
                            // Transaction breakpoint
                            if (_continue && _tx_break_points.Contains(tx.Hash))
                            {
                                Console.WriteLine($"Breakpoint hit at tx {tx.Hash.ToString()}");
                                Console.WriteLine($"Block: {_height}");
                                PrintNext();
                                _continue = false;
                                reportResults = true;
                            }
                            // Block height breakpoint
                            else if (_continue && _block_break_points.Contains(_height))
                            {
                                Console.WriteLine($"Breakpoint hit at block {_height}");
                                Console.WriteLine($"Tx: {tx.Hash.ToString()}");
                                PrintNext();
                                _continue = false;
                                reportResults = true;
                            }
                        }
                    }

                    SpinWait.SpinUntil(() => _continue == true);
                    StepInto(false);

                    if (_engine.State.HasFlag(VMState.HALT) || _engine.State.HasFlag(VMState.FAULT))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Execute() failed: {ex}");
                //_engine.State |= VMState.FAULT;
            }
            if (reportResults)
            {
                Console.WriteLine("Debug execution results:");
                Console.WriteLine($"VMState = {_engine.State}");
                Console.WriteLine($"GasConsumed = {_engine.GasConsumed}");
                Console.WriteLine("EvaluationStack = ");
                DumpStack(_engine.ResultStack);
                Console.WriteLine($"Notifications = ");
                foreach (NotifyEventArgs n in _engine.Service.Notifications)
                {
                    Console.WriteLine(ItemToJson(n.State));
                }
                Console.WriteLine("Real execution results:");
                Console.Write($"VMState = {r.VMState},");
                Console.Write($"GasConsumed = {r.GasConsumed},");
                Console.Write($"Stack = {r.Stack.Count()} items,");
                Console.WriteLine($"Notifications = {r.Notifications.Count()} items");
            }

            return !_engine.State.HasFlag(VMState.FAULT);
        }

        private bool Continue()
        {
            _continue = true;
            return true;
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            string cmd = args[0].ToLower();
            if (cmd == "" && DebuggerActive)
                if (_lastCmd == "si" || _lastCmd == "s" || _lastCmd == "so")
                    cmd = _lastCmd;
                else if (_lastCmd == "dis")
                    cmd = "nextdis";
            _lastCmd = cmd;
            switch (cmd)
            {
                case "help":
                    return OnHelp(args);
                case "debug":
                    return ActivateDebugger(args);
                case "bp":
                    return AddBreakPoint(args);
                case "tbp":
                    return AddTxBreakPoint(args);
                case "bbp":
                    return AddBlockBreakPoint(args);
                case "br":
                    return RemoveBreakPoint(args);
                case "tbr":
                    return RemoveTxBreakPoint(args);
                case "bbr":
                    return RemoveBlockBreakPoint(args);
                case "bl":
                    return ListBreakPoints();
                case "si":
                    return StepInto(true);
                case "s":
                    return StepOver();
                case "so":
                    return StepOut();
                case "c":
                    return Continue();
                case "estack":
                    return DumpStack(_engine.CurrentContext.EvaluationStack);
                case "astack":
                    return DumpStack(_engine.CurrentContext.AltStack);
                case "show":
                    return OnShow(args);
                case "dis":
                    _lineOffset = (uint)_engine.CurrentContext.InstructionPointer;
                    Console.WriteLine($"Disassembly of {_engine.CurrentContext.ScriptHash.Reverse().ToHexString()}");
                    //Console.WriteLine($"{((byte[])_engine.CurrentContext.Script).ToHexString()}");
                    return Disassemble(args);
                case "nextdis":
                    _lineOffset += 20;
                    _lastCmd = "dis";
                    return Disassemble(args);
                case "exit":
                    DebuggerActive = false;
                    _continue = true;
                    return false;

            }
            return false;
        }

        private bool OnHelp(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            Console.WriteLine($"{Name} Commands:");
            foreach (string s in commandList)
                Console.WriteLine($"\t{s}");
            return true;
        }

        private bool OnShow(string[] args)
        {
            if (args.Length < 2) return false;
            if (args[1] == "debug")
            {
                Console.WriteLine($"debugger active: {DebuggerActive}");
                return true;
            }
            return false;
        }

        private bool ActivateDebugger(string[] args)
        {
            _continue = true;
            if (args.Length < 2)
            {
                DebuggerActive = !DebuggerActive;
                return true;
            }
            else 
            {
                if (args[1] == "on")
                    DebuggerActive = true;
                else if (args[1] == "off")
                    DebuggerActive = false;
                else
                    return false;
            }
            string status = DebuggerActive ? "enabled" : "disabled";
            Console.WriteLine($"{Name} {status}");
            return true;
        }

        private bool AddBreakPoint(string[] args)
        {
            UInt160 script_hash = UInt160.Parse(args[1]);
            uint pos = uint.Parse(args[2]);
            if (!_script_break_points.TryGetValue(script_hash, out HashSet<uint> hashset))
            {
                hashset = new HashSet<uint>();
                _script_break_points.Add(script_hash, hashset);
            }
            hashset.Add(pos);

            Console.WriteLine($"Added breakpoint in {args[1]} at {args[2]}");
            return true;
        }

        private bool RemoveBreakPoint(string[] args)
        {
            UInt160 script_hash = UInt160.Parse(args[1]);
            uint pos = uint.Parse(args[2]);

            if (!_script_break_points.TryGetValue(script_hash, out HashSet<uint> hashset)) return false;
            if (!hashset.Remove(pos)) return false;
            if (hashset.Count == 0) _script_break_points.Remove(script_hash);
            Console.WriteLine($"Removed breakpoint in {args[1]} at {args[2]}");
            return true;
        }

        private bool AddTxBreakPoint(string[] args)
        {
            UInt256 tx_hash = UInt256.Parse(args[1]);
            _tx_break_points.Add(tx_hash);
            Console.WriteLine($"Added tx breakpoint in {args[1]}");
            return true;
        }

        private bool RemoveTxBreakPoint(string[] args)
        {
            UInt256 tx_hash = UInt256.Parse(args[1]);
            if (_tx_break_points.Remove(tx_hash))
            {
                Console.WriteLine($"Removed tx breakpoint in {args[1]}");
            }
            return true;
        }

        private bool AddBlockBreakPoint(string[] args)
        {
            uint block = uint.Parse(args[1]);
            _block_break_points.Add(block);
            Console.WriteLine($"Added block breakpoint in {args[1]}");
            return true;
        }

        private bool RemoveBlockBreakPoint(string[] args)
        {
            uint block = uint.Parse(args[1]);
            if (_block_break_points.Remove(block))
            {
                Console.WriteLine($"Removed block breakpoint in {args[1]}");
            }
            return true;
        }

        private bool ListBreakPoints()
        {
            Console.WriteLine("Script breakpoints:");
            foreach (KeyValuePair<UInt160, HashSet<uint>> s in _script_break_points)
            {
                string script_hash = s.Key.ToString();
                foreach (uint offset in s.Value)
                {
                    Console.WriteLine($"  {script_hash}:{offset}");
                }
            }
            Console.WriteLine("Transaction breakpoints:");
            foreach (UInt256 t in _tx_break_points)
                Console.WriteLine($"  {t.ToString()}");
            Console.WriteLine("Block breakpoints:");
            foreach (uint b in _block_break_points)
                Console.WriteLine($"  {b}");
            return true;
        }

        private bool StepInto(bool manualStep)
        {
            if (manualStep)
            {
                if (DebuggerActive)
                {
                    if (_engine.CurrentContext.InstructionPointer <= _engine.CurrentContext.Script.Length)
                    {
                        _debugger.StepInto();
                        PrintNext();
                    }
                }
            }
            else
            {
                _debugger.StepInto();
            }
            return true;
        }

        private bool StepOver()
        {
            if (DebuggerActive)
            {
                if (_engine.CurrentContext.InstructionPointer < _engine.CurrentContext.Script.Length)
                {
                    _debugger.StepOver();
                    PrintNext();
                }
            }
            return true;
        }

        private bool StepOut()
        {
            if (DebuggerActive)
            {
                if (_engine.CurrentContext.InstructionPointer < _engine.CurrentContext.Script.Length)
                {
                    _debugger.StepOut();
                    PrintNext();
                }
            }
            return true;
        }

        private void PrintNext()
        {
            var nextOpcode = _engine.CurrentContext.InstructionPointer >= _engine.CurrentContext.Script.Length ? OpCode.RET : _engine.CurrentContext.NextInstruction.OpCode;
            Console.WriteLine($"[GAS:{_engine.GasConsumed}] {_engine.CurrentContext.ScriptHash.Reverse().ToHexString()}:{_engine.CurrentContext.InstructionPointer} {DisassembleInstruction(_engine.CurrentContext.InstructionPointer)}");
            if (_engine.State.HasFlag(VMState.HALT) || _engine.State.HasFlag(VMState.FAULT))
                _continue = true;
        }

        private bool DumpStack(RandomAccessStack<StackItem> stack)
        {
            if (!DebuggerActive) return false;
            for (var i = stack.GetEnumerator(); i.MoveNext();)
            {
                StackItem item = i.Current;
                Console.WriteLine(ItemToJson(item));
            }

            return true;
        }

        private JToken ItemToJson(StackItem item)
        {
            if (item == null) return null;

            JToken value;
            string type = item.GetType().Name;

            switch (item)
            {
                case VM.Types.Boolean v: value = new JValue(v.GetBoolean()); break;
                case VM.Types.Integer v: value = new JValue(v.GetBigInteger().ToString()); break;
                case VM.Types.ByteArray v: value = new JValue(PrintableByteArray((v.GetByteArray()))); break;
                case VM.Types.Array v:
                    {
                        var jarray = new JArray();

                        foreach (var entry in v)
                        {
                            jarray.Add(ItemToJson(entry));
                        }

                        value = jarray;
                        break;
                    }
                case VM.Types.Map v:
                    {
                        var jdic = new JObject();

                        foreach (var entry in v)
                        {
                            jdic.Add(PrintableByteArray(entry.Key.GetByteArray()), ItemToJson(entry.Value));
                        }

                        value = jdic;
                        break;
                    }
                case VM.Types.InteropInterface v:
                    {
                        type = "Interop";
                        var obj = v.GetInterface<object>();

                        value = obj.GetType().Name;
                        break;
                    }
                default: throw new NotImplementedException();
            }

            return new JObject
            {
                ["type"] = type,
                ["value"] = value
            };
        }

        private string DisassembleInstruction(int offset)
        {
            DisassembleEntry e = InstructionAt(offset);
            return FormatInstruction(e);
        }

        private string FormatInstruction(DisassembleEntry e)
        {
            if (e.data != null)
            {
                if (IsAscii(e.data))
                {
                    if (e.name == "SYSCALL")
                        return $"{e.opcode:X}\t{e.name} {Encoding.UTF8.GetString(e.data)}";
                    else
                        return $"{e.opcode:X}\t{e.name} \"{Encoding.UTF8.GetString(e.data)}\"";
                }
                else
                    return $"{e.opcode:X}\t{e.name} {e.data.ToHexString()}";
            }
            else
                return $"{e.opcode:X}\t{e.name}";
        }

        private bool Disassemble(string[] args)
        {
            if (args.Length > 1)
            {
                _lineOffset = uint.Parse(args[1]);
            }
            AVMDisassemble d = NeoDisassembler.Disassemble((byte[])_engine.CurrentContext.Script);
            foreach (DisassembleEntry e in d.lines.Skip((int)_lineOffset).Take(20))

            {
                Console.WriteLine($"{e.startOfs}:\t{FormatInstruction(e)}");
            }
            return true;
        }

        public DisassembleEntry InstructionAt(int ofs)
        {
            int i = 0;
            AVMDisassemble d = NeoDisassembler.Disassemble((byte[])_engine.CurrentContext.Script);
            foreach (DisassembleEntry line in d.lines)
            {
                if (ofs >= line.startOfs && ofs <= line.endOfs)
                {
                    return line;
                }
                i++;
            }
            throw new Exception("Offset cannot be mapped");
        }

        private string PrintableByteArray(byte[] b)
        {
            if (IsAscii(b))
                return Encoding.UTF8.GetString(b);
            else
                return b.ToHexString();
        }

        private bool IsAscii(IEnumerable<byte> source)
        {
            if (source == null || source.ToArray().Length < 3) return false;
            return source.All(b => b >= 32 && b <= 127);
        }
    }
}
