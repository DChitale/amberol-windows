"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";

interface PlaylistDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (name: string) => Promise<void>;
}

export function PlaylistDialog({ open, onOpenChange, onSubmit }: PlaylistDialogProps) {
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!name.trim()) {
      return;
    }

    setBusy(true);
    await onSubmit(name.trim());
    setBusy(false);
    setName("");
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New playlist</DialogTitle>
        </DialogHeader>
        <form className="flex gap-2" onSubmit={submit}>
          <Input value={name} onChange={(event) => setName(event.target.value)} placeholder="Playlist name" autoFocus />
          <Button type="submit" variant="secondary" disabled={busy}>
            Create
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
