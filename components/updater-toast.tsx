"use client";

import { useEffect, useState } from "react";
import { ArrowUpCircle, Download, RefreshCw, X } from "lucide-react";
import { Button } from "@/components/ui/button";

// Check if running in Tauri environment
const isTauri = typeof window !== "undefined" && (window as any).__TAURI_INTERNALS__ !== undefined;

export function UpdaterToast() {
  const [isOpen, setIsOpen] = useState(false);
  const [status, setStatus] = useState<
    "idle" | "checking" | "available" | "downloading" | "installing" | "ready" | "error"
  >("idle");
  const [newVersion, setNewVersion] = useState("");
  const [releaseNotes, setReleaseNotes] = useState("");
  const [downloadProgress, setDownloadProgress] = useState(0);
  const [errorMessage, setErrorMessage] = useState("");
  const [tauriUpdateObj, setTauriUpdateObj] = useState<any>(null);

  // Check for updates
  const checkForUpdates = async () => {
    if (!isTauri) {
      console.log("[Updater] Not running in Tauri environment.");
      return;
    }

    try {
      setStatus("checking");
      const { check } = await import("@tauri-apps/plugin-updater");
      const update = await check();

      if (update) {
        setTauriUpdateObj(update);
        setNewVersion(update.version);
        setReleaseNotes(update.body || "");
        setStatus("available");
        setIsOpen(true);
      } else {
        setStatus("idle");
      }
    } catch (err: any) {
      console.error("[Updater] Check failed:", err);
      setStatus("error");
      setErrorMessage("Failed to check for updates");
    }
  };

  // Perform update
  const handleUpdate = async () => {
    if (status === "idle" || status === "checking" || status === "error") return;

    // Simulation Mode Flow
    if (!isTauri || newVersion.includes("simulated")) {
      setStatus("downloading");
      setDownloadProgress(0);

      const interval = setInterval(() => {
        setDownloadProgress((prev) => {
          if (prev >= 100) {
            clearInterval(interval);
            setStatus("installing");
            setTimeout(() => {
              setStatus("ready");
              setTimeout(() => {
                window.location.reload();
              }, 1500);
            }, 1000);
            return 100;
          }
          return prev + 10;
        });
      }, 300);
      return;
    }

    // Real Tauri Update Flow
    if (!tauriUpdateObj) return;

    try {
      setStatus("downloading");
      setDownloadProgress(0);

      let downloaded = 0;
      let contentLength = 0;

      await tauriUpdateObj.downloadAndInstall((progress: any) => {
        switch (progress.event) {
          case "Started":
            contentLength = progress.data.contentLength || 0;
            break;
          case "Progress":
            downloaded += progress.data.chunkLength;
            if (contentLength > 0) {
              const pct = Math.round((downloaded / contentLength) * 100);
              setDownloadProgress(pct);
            }
            break;
          case "Finished":
            setStatus("installing");
            break;
        }
      });

      setStatus("ready");
      
      // Delay relaunch slightly to give visual feedback
      setTimeout(async () => {
        try {
          const { relaunch } = await import("@tauri-apps/plugin-process");
          await relaunch();
        } catch (relaunchErr) {
          console.error("[Updater] Relaunch failed:", relaunchErr);
          // Fallback
          window.location.reload();
        }
      }, 1000);
    } catch (err: any) {
      console.error("[Updater] Update failed:", err);
      setStatus("error");
      setErrorMessage(err.message || "Failed to download update");
      setTimeout(() => setIsOpen(false), 4000);
    }
  };

  const handleDismiss = () => {
    setIsOpen(false);
  };

  // Simulation mode listener in devtools console
  useEffect(() => {
    if (typeof window !== "undefined") {
      (window as any).simulateUpdateToast = (version = "2.0.0-simulated", notes = "Initial stable version with Google Sans font, updated queue panel, and native app styling fixes.") => {
        setNewVersion(version);
        setReleaseNotes(notes);
        setStatus("available");
        setIsOpen(true);
        console.log("[Updater Simulation] Simulated update toast triggered.");
      };
    }

    // Auto-check on startup
    const timer = setTimeout(() => {
      void checkForUpdates();
    }, 3000); // Check 3 seconds after load

    return () => clearTimeout(timer);
  }, []);

  if (!isOpen) return null;

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex max-w-[340px] w-[calc(100%-32px)] flex-col gap-3 rounded-xl border border-white/12 bg-black/40 backdrop-blur-[24px] p-5 text-white shadow-2xl transition-all duration-300 animate-in slide-in-from-bottom-5">
      
      {/* Toast Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex h-9 w-9 items-center justify-center rounded-full bg-white/10 text-white">
            {status === "downloading" || status === "installing" ? (
              <RefreshCw className="h-5 w-5 animate-spin" />
            ) : (
              <ArrowUpCircle className="h-5 w-5" />
            )}
          </div>
          <div>
            <h4 className="text-[14px] font-bold tracking-wide">
              {status === "downloading" && "Downloading Update"}
              {status === "installing" && "Installing Update"}
              {status === "ready" && "Update Complete"}
              {status === "error" && "Update Failed"}
              {(status === "available" || status === "checking") && "Update Available"}
            </h4>
            <p className="text-[11px] text-white/50">Version {newVersion}</p>
          </div>
        </div>
        
        {status !== "downloading" && status !== "installing" && status !== "ready" && (
          <button
            onClick={handleDismiss}
            className="rounded-full p-1 text-white/70 hover:bg-white/10 hover:text-white transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {/* Toast Body / Release Notes */}
      {status === "available" && releaseNotes && (
        <div className="max-h-20 overflow-y-auto rounded-lg bg-white/5 p-2 text-xs text-white/70 leading-normal scrollbar-thin">
          {releaseNotes}
        </div>
      )}

      {/* Error Message */}
      {status === "error" && errorMessage && (
        <div className="rounded-lg bg-red-500/10 p-2.5 text-xs text-red-400 border border-red-500/15">
          {errorMessage}
        </div>
      )}

      {/* Progress Bar & Status Text */}
      {(status === "downloading" || status === "installing" || status === "ready") && (
        <div className="flex flex-col gap-1.5 mt-1">
          <div className="h-1.5 w-full rounded-full bg-white/10 overflow-hidden">
            <div
              className={`h-full transition-all duration-300 ${
                status === "ready" ? "bg-white/80" : "bg-white/70"
              }`}
              style={{
                width: `${status === "ready" || status === "installing" ? 100 : downloadProgress}%`,
              }}
            />
          </div>
          <div className="flex justify-between text-[10px] font-medium text-white/50">
            <span>
              {status === "downloading" && `Downloading files...`}
              {status === "installing" && `Extracting and installing...`}
              {status === "ready" && `Relaunching Amberol...`}
            </span>
            {status === "downloading" && <span>{downloadProgress}%</span>}
          </div>
        </div>
      )}

      {/* Actions */}
      {status === "available" && (
        <div className="mt-1 flex gap-2 w-full">
          <Button
            onClick={handleUpdate}
            variant="secondary"
            className="flex-1 text-xs font-semibold py-2.5 h-auto rounded-md"
          >
            <Download className="h-3.5 w-3.5 mr-1" />
            Update Now
          </Button>
          <Button
            onClick={handleDismiss}
            variant="subtle"
            className="flex-1 text-xs font-semibold py-2.5 h-auto rounded-md"
          >
            Maybe Later
          </Button>
        </div>
      )}
    </div>
  );
}
