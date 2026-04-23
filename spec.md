# HttpROS Technical Specification
## Unified HTTP Routing & Network Operating System

HttpROS is a mission-critical, native HTTP orchestration platform. It integrates a high-performance Data Plane with a sophisticated, hierarchical Control Plane (CLI), providing an "Infrastructure-as-Code" experience through an interactive Network OS interface.

---

## 1. Solution Architecture (Enterprise Layout)
The project follows the standard .NET Solution architecture for separation of concerns and testability.

### 1.1 Project Structure
- **`HttpROS.sln`**: Main solution file.
- **`src/HttpROS.HttpROS/`**: The core application project.
- **`src/HttpROS.Test/`**: XUnit test suite for automated validation.
- **`Data/`**: (Root level) Hierarchical database for persistent configuration state.

### 1.2 Control Plane (CLI Engine)
- **Interactive Shell**: Custom-built loop with contextual intelligence.
- **Navigation Primitives**: `top` (Root), `return` (Global Config), `exit` (Back).
- **Validation Layer**: Real-time input sanitization for domains, IPs, and rate-limits.

---

## 2. Data Management (State Persistence)
HttpROS utilizes a declarative, file-based state model.

### 2.1 Persistence Hierarchy
- **Storage Mapping**:
  - `Data/proxy/`, `Data/static/`, `Data/redirect/`: Route definitions.
  - `Data/certs/`: Cryptographic assets.
  - `Data/error-pages/`: Diagnostic assets.
- **Atomic Operations**: All state changes are committed via atomic file writes to prevent configuration corruption.

### 2.2 Route Lifecycle & Deletion
- **Creation**: Commands `proxy`, `static`, or `redirect` followed by a domain.
- **Modification**: Entering route-config mode for an existing domain.
- **Deletion**: The `no` prefix applied to route commands (e.g., `no proxy example.com`) will permanently remove the configuration file and trigger a Data Plane reload.

### 2.3 Target Format Standards
- **Proxy/Static**: Supports `IP`, `IP:Port`, or `Hostname`. If the protocol is omitted, `http://` is assumed as the backend scheme.
- **Redirect**: Supports full `URLs` (e.g., `https://google.com`). 
  - **Custom Codes**: Supports `code <301|302|307|308>` (Default: 302). This status code is returned with the `Location` header.

---

## 3. Comprehensive Feature Matrix (Roadmap & Status)

| Feature | Proxy/Static | Redirect | Description |
| :--- | :---: | :---: | :--- |
| **Target** | ✅ | ✅ | Destination IP/URL |
| **Wildcards (*.)** | ✅ | ✅ | Catch-all subdomains |
| **SSL (SNI)** | ✅ | ✅ | HTTPS Support |
| **Auth** | ✅ | ✅ | Basic Authentication protection |
| **IP Filter** | ✅ | ✅ | Blacklist/Whitelist protection |
| **Rate Limit** | ✅ | ✅ | Throttling/DoS protection |
| **CORS** | ✅ | ✅ | Cross-Origin headers |
| **Redirection Code** | ❌ | ✅ | 301, 302, 307, 308 |
| **Load Balancer** | ✅ | ❌ | Multiple upstreams/Health checks |
| **Gzip** | ✅ | ❌ | Payload compression |
| **Websockets** | ✅ | ❌ | Bi-directional socket proxying |
| **Error Pages** | ✅ | ❌ | Custom HTML for HTTP status codes |

---

## 4. Testing & Quality Assurance
HttpROS maintains a high-integrity codebase with a target of maximum CLI test coverage.

### 4.1 Automated Test Suite
- **Engine Validation**: Testing of `CliEngine` state transitions and command routing.
- **Persistence Integrity**: Validation of `StorageService` file operations and data consistency.
- **Validation Logic**: Unit testing of `ValidationService` against RFC compliance and asset existence.
- **Comprehensive Command Coverage**: Every command flag (Gzip, Websockets, CORS, Auth, etc.) and its `no` variant is verified.
- **UX Interaction Standards**: All commands, including those using the `no` prefix, must support contextual help (`?`) and Tab-Completion for discovery.

### 4.2 Coverage Goals
- **Control Plane**: 100% coverage of navigation and command dispatching logic.
- **Data Plane Persistence**: 100% coverage of serialization and atomic storage operations.
- **Validation Layer**: 100% coverage of all regex and integrity checks.

---

## 5. Operational Environment
- **Runtime**: .NET 10.0 (Core Engine).
- **Deployment**: Secure Docker image with integrated SSH Management (Port 50022).
- **Storage**: Single persistent volume mapping (`./Data:/app/Data`).
