import { api } from '../../services/api';
import type { NotificationPage, NotificationPreference } from './types';

export const notificationService = {
  list: (): Promise<NotificationPage> => api.getNotifications(false, 1, 30),
  preferences: (): Promise<NotificationPreference[]> => api.getNotificationPreferences(),
  markRead: (id: string) => api.markNotificationRead(id),
  markAllRead: () => api.markAllNotificationsRead(),
  setPreference: (preference: NotificationPreference) => api.setNotificationPreference(
    preference.type, preference.inAppEnabled, preference.emailEnabled),
};
