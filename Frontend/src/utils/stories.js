import axios from "axios";
import { getApiUrl } from "./api";

export const STORIES_CHANGED_EVENT = "stories:changed";
export const STORY_IMAGE_DURATION_MS = 5000;

export async function fetchStoryFeed(viewerId) {
  const response = await axios.get(getApiUrl("/api/Stories/feed"));
  return response.data || [];
}

export async function fetchUserStories(userId, viewerId) {
  const response = await axios.get(getApiUrl(`/api/Stories/user/${userId}`));
  return response.data || [];
}

/** Uploads to the API, which stores the media in the "stories" Azure Blob container. */
export async function createStory(userId, file, caption, onProgress, music) {
  const formData = new FormData();
  formData.append("media", file);
  if (caption) formData.append("caption", caption);
  if (music?.file) formData.append("music", music.file);
  if (music?.title) formData.append("musicTitle", music.title);
  if (music?.artist) formData.append("musicArtist", music.artist);
  const response = await axios.post(getApiUrl("/api/Stories"), formData, {
    onUploadProgress: (event) => event.total && onProgress?.(event.loaded / event.total),
  });
  window.dispatchEvent(new CustomEvent(STORIES_CHANGED_EVENT));
  return response.data;
}

export async function markStoryViewed(storyId, viewerId) {
  await axios.post(getApiUrl(`/api/Stories/${storyId}/view`));
}

export async function fetchStoryViewers(storyId, userId) {
  const response = await axios.get(getApiUrl(`/api/Stories/${storyId}/viewers`));
  return response.data || [];
}

export async function deleteStory(storyId, userId) {
  await axios.delete(getApiUrl(`/api/Stories/${storyId}`));
  window.dispatchEvent(new CustomEvent(STORIES_CHANGED_EVENT));
}
