FROM mcr.microsoft.com/dotnet/sdk:5.0

WORKDIR /app/CompileTimeMethodExecutionGenerator.Generator
COPY ./CompileTimeMethodExecutionGenerator.Generator/CompileTimeMethodExecutionGenerator.Generator.csproj ./
RUN dotnet restore

WORKDIR /app/CompileTimeMethodExecutionGenerator.Example
COPY ./CompileTimeMethodExecutionGenerator.Example/CompileTimeMethodExecutionGenerator.Example.csproj ./
RUN dotnet restore

WORKDIR /app

COPY . ./

RUN dotnet build ./CompileTimeMethodExecutionGenerator.Example/CompileTimeMethodExecutionGenerator.Example.csproj --no-restore

CMD dotnet run --project ./CompileTimeMethodExecutionGenerator.Example/CompileTimeMethodExecutionGenerator.Example.csproj