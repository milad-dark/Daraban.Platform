export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  activeEntityId: string;
  roles: string[];
  tokenVersion: number;
}