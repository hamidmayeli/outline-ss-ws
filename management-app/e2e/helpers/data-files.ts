import fs from 'node:fs/promises';
import path from 'node:path';

const runtimeDataDir = path.resolve(process.cwd(), 'runtime-data');
const clientsFile = path.join(runtimeDataDir, 'clients.json');
const usersFile = path.join(runtimeDataDir, 'users.json');
const refreshTokensFile = path.join(runtimeDataDir, 'refresh-tokens.json');
const outlineConfigFile = path.join(runtimeDataDir, 'outline-config.yaml');

type SeedClient = {
  Id: string;
  Name: string;
  Secret: string;
  Cipher: string;
  Limit: number | null;
  IsActive: boolean;
  IsSingleConnection: boolean;
  AccessKeyId: number;
};

type SeedUser = {
  Id: string;
  Username: string;
  PasswordHash: string;
  CreatedAt: string;
  UpdatedAt: string | null;
};

const ensureRuntimeDataDir = async () => {
  await fs.mkdir(runtimeDataDir, { recursive: true });
};

export async function resetBackendDataFiles() {
  await ensureRuntimeDataDir();

  await Promise.all([
    fs.rm(clientsFile, { force: true }),
    fs.rm(usersFile, { force: true }),
    fs.rm(refreshTokensFile, { force: true }),
  ]);
}

export async function seedClients(clients: SeedClient[]) {
  await ensureRuntimeDataDir();
  await fs.writeFile(clientsFile, JSON.stringify(clients), 'utf8');
}

export async function seedUsers(users: SeedUser[]) {
  await ensureRuntimeDataDir();
  await fs.writeFile(usersFile, JSON.stringify(users), 'utf8');
}

export async function readRuntimeFile(fileName: string): Promise<string> {
  const filePath = path.join(runtimeDataDir, fileName);
  return fs.readFile(filePath, 'utf8');
}

export async function readSeededClients(): Promise<SeedClient[]> {
  const content = await fs.readFile(clientsFile, 'utf8');
  return JSON.parse(content) as SeedClient[];
}

export async function readSeededUsers(): Promise<SeedUser[]> {
  try {
    const content = await fs.readFile(usersFile, 'utf8');
    return JSON.parse(content) as SeedUser[];
  } catch {
    return [];
  }
}

export async function readOutlineConfig(): Promise<string> {
  try {
    return await fs.readFile(outlineConfigFile, 'utf8');
  } catch {
    return '';
  }
}

export const seedSamples = {
  clientsWithMixedStatus: [
    {
      Id: 'e2e-client-1',
      Name: 'E2E Client Alpha',
      Secret: 'alpha-secret-key',
      Cipher: 'chacha20-ietf-poly1305',
      Limit: null,
      IsActive: true,
      IsSingleConnection: false,
      AccessKeyId: 1,
    },
    {
      Id: 'e2e-client-2',
      Name: 'E2E Client Beta',
      Secret: 'beta-secret-key',
      Cipher: 'chacha20-ietf-poly1305',
      Limit: 1073741824,
      IsActive: false,
      IsSingleConnection: false,
      AccessKeyId: 2,
    },
  ] satisfies SeedClient[],
  singleConnectionClient: [
    {
      Id: 'e2e-single-conn',
      Name: 'E2E Single Conn',
      Secret: 'single-conn-secret',
      Cipher: 'chacha20-ietf-poly1305',
      Limit: null,
      IsActive: true,
      IsSingleConnection: true,
      AccessKeyId: 3,
    },
  ] satisfies SeedClient[],
};
