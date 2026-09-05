export interface PlatformNotification {
  id: string;
  type: number;
  typeName: string;
  title: string;
  message: string;
  link?: string | null;
  isRead: boolean;
  readAt?: string | null;
  createdAt: string;
}

export interface NotificationPage {
  items: PlatformNotification[];
  total: number;
  unread: number;
  page: number;
  pageSize: number;
}

export interface NotificationPreference {
  type: number;
  name: string;
  inAppEnabled: boolean;
  emailEnabled: boolean;
}
