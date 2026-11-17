import { defineConfig } from "vite";
import {readFileSync} from "node:fs";
import { Agent } from "node:https";
import { homedir } from "node:os";
import { join } from "node:path";

export default defineConfig(({ command }) => {
    if (command === "build"){
        return {
            base: "/client",
            build: {
                outDir: "../Messenger.Web/wwwroot/client",
                emptyOutDir: true,
            },
        };
    }

    const certDir = join(homedir(), ".aspnet", "messenger-vite");
    const cert = readFileSync(join(certDir, "localhost.pem"));
    const key = readFileSync(join(certDir, "localhost.key"));

    return {
        server: {
            host: "localhost",
            port: 5173,
            strictPort: true,
            https: {cert, key},
            proxy: {
                "/api": {
                    target: "https://localhost:7178",
                    changeOrigin: true,
                    agent: new Agent({ca: cert}),
                },
                "/hubs": {
                    target: "https://localhost:7178",
                    changeOrigin: true,
                    ws: true,
                    agent: new Agent({ca: cert}),
                }
            }
        }
    }
});