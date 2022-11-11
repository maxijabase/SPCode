using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using SourcepawnCondenser;
using SourcepawnCondenser.Tokenizer;
using static SPCode.Utils.SPSyntaxTidy.SPSyntaxTidy;

namespace SPCodeBenchmarks;

[Config(typeof(Config))]
[MemoryDiagnoser]
public class CondenserBench
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.MediumRun
                .WithLaunchCount(1)
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithId("InProcess"));
        }
    }

    private readonly string _text;

    public CondenserBench()
    {
        _text = File.ReadAllText("sourcepawn/nativevotes.inc");
    }
    
    [Benchmark]
    public void Condense()
    {
        //Tokenizer.TokenizeString(_text, true);

        var condenser =
            new Condenser(_text, "test"); // The biggest thing I found

        condenser.Condense();
    }
}