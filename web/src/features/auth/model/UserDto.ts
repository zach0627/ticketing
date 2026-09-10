import type { UserRole } from './UserRole';

export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}
