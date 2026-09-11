import { getMe, type MeResponse } from "./api";

let snapshot: MeResponse | null = null;
let inflight: Promise<MeResponse> | null = null;

export function readSession(): MeResponse | null {
  return snapshot;
}

export function clearSession(): void {
  snapshot = null;
  inflight = null;
}

export function loadSession(): Promise<MeResponse> {
  if (snapshot) {
    return Promise.resolve(snapshot);
  }
  if (inflight) {
    return inflight;
  }
  inflight = getMe()
    .then((me) => {
      snapshot = me;
      inflight = null;
      return me;
    })
    .catch((error) => {
      inflight = null;
      throw error;
    });
  return inflight;
}
