# No registry pull; tools/build_worker.py assembles a hashed, credential-free local SDK snapshot.
FROM scratch
COPY rootfs/ /
ENV PATH=/opt/dotnet:/bin DOTNET_ROOT=/opt/dotnet DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /app
USER 10001:10001
ENTRYPOINT ["dotnet", "/app/CodeTurtleEngine.Worker.dll"]
