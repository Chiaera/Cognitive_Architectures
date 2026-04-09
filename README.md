# Pseudo‑Haptic Feedback Framework with High‑Precision Hand Tracking in AR 

## Project Overview
This project explores whether it is possible to induce sensations such as **weight, stiffness or texture** in Augmented Reality using **only visual manipulation**, without any physical haptic hardware.

The goal is to understand how visual–proprioceptive conflicts can generate convincing pseudo‑haptic illusions; using **MANUS Quantum Metagloves** for precise finger tracking and a **Magic Leap 2** headset for AR.


## Tasks:
1) Build an AR test environment in **Unity** where virtual objects are overlaid on a real workspace
2) Stream real‑time hand‑tracking from **MANUS Quantum Metagloves** to Unity (via MANUS Core SDK)
3) Implement visual manipulation strategies simulating: 
    - weight
    - rigidity
    - softness
    - stickiness
4) Implement **per‑finger mismatch control** to independently modulate visuo‑proprioceptive discrepancies
5) Conduct a **within‑subjects user study** measuring, for each effect type:
    - illusion threshold
    - perceived intensity
    - break‑point 
6) Collect and analyze:
    - **Subjective data:** Likert scales, free‑text reports
    - **Objective data:** task completion time, grasp accuracy, error rate
7) Compare results with:
    - a baseline condition (no visual manipulation)
    - existing VR pseudo‑haptic benchmarks from literature

## Software and Hardware Requirements
- **Unity 2022 LTS**
- **MANUS Core SDK**
- **Magic Leap 2 SDK** (OpenXR)
- **Python** (statistical analysis)
- **R** or **JASP** (psychophysics)

## Research Background
- pseudo‑haptic feedback 
- visuo‑proprioceptive conflict models
- AR perception and multisensory integration
- psychophysical methods for measuring haptic illusion
- per‑finger proprioception and fine‑motor perception

## Deliverables
- [ ] Unity AR application with configurable pseudo‑haptic effects
- [ ] Full experimental protocol
- [ ] Raw dataset of user responses
- [ ] Statistical analysis report