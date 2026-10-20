import axios from "axios";
import { getApiUrl } from "./api";

export const TERMINAL_STATUSES = ["Succeeded", "Failed"];

export async function fetchStudioStatus() {
  const response = await axios.get(getApiUrl("/api/Images/status"));
  return response.data;
}

/** Queues a ComfyUI job (API -> Service Bus -> Azure Function -> ComfyUI -> Blob Storage). */
export async function generateImage({ prompt, aspect, style, seed }) {
  const response = await axios.post(getApiUrl("/api/Images"), { prompt, aspect, style, seed });
  return response.data;
}

export async function fetchImageJob(id) {
  const response = await axios.get(getApiUrl(`/api/Images/${id}`));
  return response.data;
}

export async function fetchMyImages(take = 24) {
  const response = await axios.get(getApiUrl(`/api/Images/mine?take=${take}`));
  return response.data || [];
}

export async function shareImage(id, caption) {
  const response = await axios.post(getApiUrl(`/api/Images/${id}/share`), { caption });
  return response.data.postId;
}
