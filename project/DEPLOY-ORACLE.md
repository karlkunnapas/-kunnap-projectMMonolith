# Deploying the backend to Oracle Cloud (Always Free)

This deploys only `project/` (WebApp API + Postgres/PostGIS), fronted by Caddy for automatic HTTPS.

## 1. Create the Oracle Cloud account

1. Go to https://www.oracle.com/cloud/free/ and sign up. Requires a real phone number and a card for
   identity verification (you will not be charged if you stay on Always Free resources).
2. Pick a home region close to you — this can't be changed later for your tenancy.

## 2. Create the VM (Always Free ARM shape)

1. Console → **Compute → Instances → Create Instance**.
2. Image: **Canonical Ubuntu 24.04** (aarch64/ARM build).
3. Shape: **Ampere VM.Standard.A1.Flex** — set 2 OCPU / 12 GB RAM (Always Free covers up to 4 OCPU / 24 GB
   total across all A1 instances).
4. Networking: use the default VCN, keep "Assign a public IPv4 address" checked.
5. Add your SSH public key (or let Oracle generate a key pair — download the private key).
6. Create.

## 3. Open the firewall (two layers — both are required)

**a) Security list / NSG** (Console → your VCN → Security Lists → default → Ingress Rules):
- Add rule: source `0.0.0.0/0`, TCP, destination port **80**
- Add rule: source `0.0.0.0/0`, TCP, destination port **443**

**b) OS-level firewall on the instance itself** (Oracle's Ubuntu image ships with iptables rules that block
inbound traffic even after the security list is opened):

```bash
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

## 4. DNS

Point a domain or subdomain's A record at the instance's public IP. If you don't have a domain handy for
this test, you can use a free wildcard-DNS host like `<public-ip>.nip.io` — Caddy can still issue a
Let's Encrypt cert for that.

## 5. Install Docker on the VM

```bash
ssh ubuntu@<public-ip>
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker
```

## 6. Get the code onto the VM

```bash
git clone <your-repo-url>
cd kunnap-project-m-monolith/project
```

(Only `project/` is needed — do not clone/run `WebAppClient`.)

## 7. Configure secrets

```bash
cp .env.example .env
nano .env
```

Fill in:
- `POSTGRES_PASSWORD` — long random value (e.g. `openssl rand -base64 32`)
- `JWT_KEY` — long random value, at least 32 chars (e.g. `openssl rand -base64 48`)
- `DOMAIN` — the domain/subdomain from step 4

## 8. Start it

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Caddy will automatically request a Let's Encrypt certificate for `DOMAIN` on first request (ports 80/443
must already be reachable from step 3 for the ACME challenge to succeed).

## 9. Verify

```bash
docker compose -f docker-compose.prod.yml ps
curl -I https://<your-domain>
```

Check logs if anything looks wrong:

```bash
docker compose -f docker-compose.prod.yml logs -f chargepanel-modular-monolith-webapp
```

## Notes

- `docker-compose.prod.yml` no longer publishes port 8083 to the host — only Caddy (80/443) is exposed
  publicly; the API is reached only through the reverse proxy.
- `restart: unless-stopped` on all services means they come back after a VM reboot as long as the Docker
  daemon itself starts on boot (`sudo systemctl enable docker`, on by default with get.docker.com).
- To update after a code change: `git pull && docker compose -f docker-compose.prod.yml --env-file .env up -d --build`.
