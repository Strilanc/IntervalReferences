IntervalReferences
==================

An *interval reference* keeps a whole section of an array alive, while allowing the rest to be reclaimed if no other references cover it.

The code in this repository is an mockup of how interval references would work, algorithmically, based on using a balanced binary search tree to track nesting depth and detect holes.

An animation of the idea:

![Slicing Collection Animation](http://i.imgur.com/TJYnNPC.gif)
