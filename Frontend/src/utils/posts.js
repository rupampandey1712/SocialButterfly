import axios from "axios";
import { getApiUrl } from "./api";

export const POST_CREATED_EVENT = "posts:created";

export async function fetchFeed(viewerId, skip = 0, take = 10) {
  const params = new URLSearchParams({ skip, take });
  const response = await axios.get(getApiUrl(`/api/Post/feed?${params}`));
  return response.data || [];
}

export async function fetchUserPosts(userId, viewerId) {
  const response = await axios.get(getApiUrl(`/api/Post/user/${userId}`));
  return response.data || [];
}

export async function fetchPost(postId, viewerId) {
  const response = await axios.get(getApiUrl(`/api/Post/details/${postId}`));
  return response.data;
}

export async function toggleLike(postId, userId) {
  const response = await axios.post(getApiUrl(`/api/Likes/toggle?postId=${postId}`));
  return response.data;
}

export async function fetchComments(postId) {
  const response = await axios.get(getApiUrl(`/api/Comment/${postId}`));
  return response.data || [];
}

export async function addComment(postId, userId, text) {
  const response = await axios.post(getApiUrl("/api/Comment"), { postId, userId, text });
  return response.data;
}

export async function deletePost(postId, userId) {
  await axios.delete(getApiUrl(`/api/Post/${postId}`));
}

/**
 * The author comes from the JWT. `music` = { file, title, artist } adds a soundtrack to a
 * photo/video post; for a song post the main file is audio and title/artist describe it.
 */
export async function createPost(userId, description, file, music, onProgress) {
  const formData = new FormData();
  formData.append("Description", description);
  if (file) formData.append("ImageFile", file);
  if (music?.file) formData.append("MusicFile", music.file);
  if (music?.title) formData.append("MusicTitle", music.title);
  if (music?.artist) formData.append("MusicArtist", music.artist);
  const response = await axios.post(getApiUrl("/api/Post"), formData, {
    onUploadProgress: (event) => event.total && onProgress?.(event.loaded / event.total),
  });
  window.dispatchEvent(new CustomEvent(POST_CREATED_EVENT, { detail: response.data }));
  return response.data;
}

export async function fetchProfile(userId, viewerId) {
  const response = await axios.get(getApiUrl(`/api/Account/${userId}/profile`));
  return response.data;
}

export async function sendFriendRequest(senderId, receiverId) {
  await axios.post(getApiUrl("/api/Friends"), { senderId, receiverId });
}

export async function acceptFriendRequest(senderId, receiverId) {
  await axios.post(getApiUrl(`/api/Friends/${senderId}`));
}

export async function fetchNotifications(userId) {
  const response = await axios.get(getApiUrl("/api/Notifications/me"));
  return response.data || [];
}

export async function fetchUnreadNotificationCount(userId) {
  const response = await axios.get(getApiUrl("/api/Notifications/me/unread-count"));
  return response.data?.count ?? 0;
}

export async function markNotificationsRead(userId) {
  await axios.post(getApiUrl("/api/Notifications/me/read"));
}
