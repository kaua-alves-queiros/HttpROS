# HttpROS Technical Specification
## Reverse Proxy & Redirect Engine

HttpROS is a reverse proxy and redirection engine built with .NET 10 and YARP. It features a CLI-based management interface (Control Plane) and a high-performance routing engine (Data Plane) that handles proxying, static file hosting, and HTTP redirects.

---

## 1. Project Architecture
The solution is divided into the core engine and an automated test suite.

### 1.1 Project Structure
- **`HttpROS.sln`**: Main solution file.
- **`src/HttpROS.HttpROS/`**: Core application (Proxy Engine + CLI).
- **`src/HttpROS.Test/`**: XUnit tests.
- **`Data/`**: Persistent JSON configuration files.

### 1.2 Management Interface (CLI)
- **Interactive Shell**: Command-loop with support for contextual navigation.
- **Navigation**: `top` (Root), `return` (Global Config), `exit` (Back).
- **Validation**: Real-time checks for domains, IPs, and rate-limit formats.

---

## 2. Data Persistence
HttpROS stores all configuration in JSON files.

### 2.1 File Structure
- **Storage Paths**:
  - `Data/proxy/`, `Data/static/`, `Data/redirect/`: Route configs.
  - `Data/certs/`: SSL/TLS certificates.
  - `Data/error-pages/`: Custom HTML error pages.
- **Save Logic**: Configuration changes are saved to disk immediately, triggering an engine reload.

### 2.2 Route Lifecycle
- **Creation**: Use `proxy`, `static`, or `redirect` followed by the domain.
- **Deletion**: Use the `no` prefix (e.g., `no proxy example.com`) to delete the config file.

### 2.3 Targets & Redirection
- **Proxy/Static Targets**: Supports `IP`, `IP:Port`, or `Hostname`. Defaults to `http://`.
- **Redirects**: Supports full URLs.
  - **Codes**: Supports HTTP 301, 302, 307, 308 (Default: 302).

---

## 3. Feature Matrix

| Feature | Proxy/Static | Redirect | Description |
| :--- | :---: | :---: | :--- |
| **Target** | ✅ | ✅ | Destination IP/URL |
| **Wildcards (*.)** | ✅ | ✅ | Catch-all subdomains |
| **SSL (SNI)** | ✅ | ✅ | HTTPS Support |
| **Auth** | ✅ | ✅ | Basic Authentication |
| **IP Filter** | ✅ | ✅ | Blacklist/Whitelist |
| **Rate Limit** | ✅ | ✅ | Request throttling |
| **CORS** | ✅ | ✅ | Cross-Origin headers |
| **Redirection Code** | ❌ | ✅ | 301, 302, 307, 308 |
| **Load Balancer** | ✅ | ❌ | Upstreams & Health checks |
| **Gzip** | ✅ | ❌ | Compression |
| **Websockets** | ✅ | ❌ | WS/WSS Proxying |
| **Error Pages** | ✅ | ❌ | Custom HTML status pages |

---

## 4. Testing & Quality
The project uses automated tests to ensure configuration integrity and CLI stability.

### 4.1 Test Areas
- **CLI Logic**: Validation of state transitions and command parsing.
- **Storage**: Verification of file I/O and JSON serialization.
- **Validation**: Regex checks for RFC compliance.
- **CI/CD**: Automated build and test on every push to main.

---

## 5. Environment
- **Runtime**: .NET 10.0.
- **Deployment**: Docker-based with optional SSH management.
- **Storage**: Persistent volume mapping on `/app/Data`.
