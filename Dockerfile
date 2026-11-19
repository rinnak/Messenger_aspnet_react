FROM node:24-bookworm-slim AS frontend
WORKDIR /frontend
COPY src/messenger-client/package.json src/messenger-client/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm \
    npm ci --include=dev --prefer-offline --fetch-retries=4 \
    --maxsockets=3 --no-audit --no-fund --loglevel=verbose
COPY src/messenger-client/ ./
RUN npm run build -- --outDir /ui

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /source
COPY src/ ./src/
RUN dotnet publish src/Messenger.Web/Messenger.Web.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /publish/ ./
COPY --from=frontend /ui/ ./wwwroot/client/
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Messenger.Web.dll"]