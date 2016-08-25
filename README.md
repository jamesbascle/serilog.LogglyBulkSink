# Serilog.LogglyBulkSink

A Bulk Http Loggly Sink for [Serilog](https://github.com/serilog/serilog)
That supports upto 10000x more efficient logging to loggly by making use of the Loggly Bulk Http endpoint.

##Build

[![Build status](https://ci.appveyor.com/api/projects/status/p3a6vkgfxqlypfnr/branch/master?svg=true)](https://ci.appveyor.com/project/jamesbascle/serilog-logglybulksink/branch/master)

##News

8-24-2016 Release v1.0 and upgrading to Serilog 2.0!

## Setup

Install via nuget
```
Install-Package Serilog.LogglyBulk
```

## Usage

```csharp
 var logglyKey = "125125125125125125125125"; // whatever your loggly key is
 var tags = new []{"PROD", "MyCoolApp"};
 var logger = new LoggerConfiguration()
      .MinimumLevel.Verbose()
      .WriteTo.LogglyBulk(logglyKey, tags, batchPostingLimit: 10000, period: TimeSpan.FromSeconds(10))
      .CreateLogger();

```

