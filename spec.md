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

---

## 3. Comprehensive Feature Matrix (Roadmap & Status)

### 3.1 Core Routing & Protocol Support
- [x] **Native Reverse Proxy**: High-performance L7 request forwarding.
- [x] **Static Content Hosting**: Dedicated mode for serving local web assets.
- [x] **HTTP Redirection**: Advanced URL mapping and status code control.
- [x] **Input Integrity**: Automated validation of domains, targets, and file paths.

### 3.2 Traffic Orchestration (Load Balancing)
- [x] **Algorithms**: `round-robin`, `least-connections`, `ip-hash`.
- [x] **Session Persistence**: Native `sticky-session` support (Cookie affinity).
- [x] **Health Checks**: Passive/Active backend monitoring (`health-check` command).

### 3.3 Security & Edge Protection
- [x] **SSL/TLS Management**: SNI support with Let's Encrypt (Automated ACME renewal) and Manual certs.
- [x] **AAA (Identity)**: Integrated `Basic Authentication` per route.
- [x] **IP-based Filtering**: Hierarchical `ip whitelist` and `ip blacklist` control.
- [x] **Shielding**: Per-route `Rate Limit` control.

---

## 4. Testing & Quality Assurance
HttpROS maintains a high-integrity codebase with a target of maximum CLI test coverage.

### 4.1 Automated Test Suite
- **Engine Validation**: Testing of `CliEngine` state transitions and command routing.
- **Persistence Integrity**: Validation of `StorageService` file operations and data consistency.
- **Validation Logic**: Unit testing of `ValidationService` against RFC compliance and asset existence.
- **Comprehensive Command Coverage**: Every command flag (Gzip, Websockets, CORS, Auth, etc.) and its `no` variant is verified.

### 4.2 Coverage Goals
- **Control Plane**: 100% coverage of navigation and command dispatching logic.
- **Data Plane Persistence**: 100% coverage of serialization and atomic storage operations.
- **Validation Layer**: 100% coverage of all regex and integrity checks.

---

## 5. Operational Environment
- **Runtime**: .NET 10.0 (Core Engine).
- **Deployment**: Secure Docker image with integrated SSH Management (Port 50022).
- **Storage**: Single persistent volume mapping (`./Data:/app/Data`).
