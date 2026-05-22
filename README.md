# CarSimulatorWithCAN

A physical car simulation project built in Unity, integrating **CAN bus communication protocols** with an intelligent **Autonomous Vehicle (AV) traffic yielding system** based on the real-world traffic flows around National Taipei University of Technology (NTUT).

## 🚀 Key Features

### 1. CAN Bus Message Simulation
- **Vehicle Behavior Modeling**: Simulates real-time powertrain, velocity, and sensor data (e.g., radar, traffic light sensing) packaged into CAN messages.
- **Emergency Signaling via CAN**: When an emergency vehicle (ambulance) approaches, a specific CAN ID warning message is broadcasted, triggering the immediate response of nearby NPC vehicles.

### 2. Intelligent Yielding System (NTUT Context)
- **Dynamic Space Yielding Algorithm**: Upon receiving the CAN emergency signal, NPCs calculate road boundaries using **Raycasting** and compute a smooth S-shaped path via **Vector Mathematics** to clear the center lane (Moses Effect).
- **V2X & Traffic Light Integration**: Features a **real-world traffic light timing model** based on the actual intersections near NTUT (e.g., Zhongxiao East Road). NPCs combine radar inputs and signal countdowns to safely decide whether to accelerate or brake at complex junctions.

### 3. Modular & Optimized Architecture
- **Object-Oriented Design (OOP)**: Strictly structured scripts to ensure high code readability and maintainability.
- **Performance Optimization**: Solved the bottleneck of concurrent NPC path recalculations by shifting from global pathfinding to localized vector corrections, maintaining a stable FPS under high-density traffic.

## 🛠️ System Architecture & Environment
- **Game Engine**: Unity 3D
- **Programming Language**: C#
- **Core Concepts**: CAN Bus Communication, Vector Mathematics, Multi-Sensor Fusion, Object-Oriented Programming

## 📦 How to Run
1. **Clone the repo** ```bash
   git clone [https://github.com/ZAPEinthezone/CarSimulatorWithCAN.git](https://github.com/ZAPEinthezone/CarSimulatorWithCAN.git)
