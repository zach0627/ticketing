import type { UserDto } from './UserDto';

export interface AuthResponse {
  accessToken: string;
  user: UserDto;
}
