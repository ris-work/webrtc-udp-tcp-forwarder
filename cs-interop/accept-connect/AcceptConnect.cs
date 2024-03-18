﻿<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="5.0.0" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="3.0.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="3.1.1" />
    <PackageReference Include="SIPSorcery.WebSocketSharp" Version="0.0.1" />
    <PackageReference Include="SIPSorcery" Version="6.2.0" />
  </ItemGroup></Project>
