## RNL Utilities 
Supplementary code for the CHI 2020 paper: "Improving Virtual Reality Ergonomics through Reach-Bounded Non-Linear Input Amplification"

### Usage
Assign these to a new GameObject and give the reference to that GameObject to whatever part of your code requires it.

### HandTranslationAmplifier.cs
This does the main work for amplifying user input in the scene. You supply it with the positions of the user's hands and shoulders (can be inferred) and it will amplify user input after a short calibration process. This uses an inverted sphere for raycasting, but I've inlcuded the 3D model for the sphere I used (invertedsphere.fbx).

### RULACalculation.cs
Takes in the user's body positions and calculates RULA. It's important that you use real (read: non-inferred) points here, or else the values will be skewed.
