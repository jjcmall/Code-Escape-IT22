# CODE ESCAPE: THE MISSING KID - 70% Pre-Final Defense: What to Say

Target length: about 12 minutes (8 min talk + 4 min demo), then questions.
Text in [brackets] must be filled in by the team with true information before the defense.

---

## Slide 1 - Title (20 seconds)
**Say:**
"Good [morning/afternoon], panel. We are [Group name]. Our capstone is **Code Escape: The Missing Kid**, an educational 2.5D adventure game that teaches beginners Python by having them write and run code. Today we present our progress at about 70 percent."

## Slide 2 - The problem and our goal (45 seconds)
**Say:**
"Many beginners find programming abstract and boring, and they give up early. Our game puts coding inside a story, so students learn a concept, use it to solve a puzzle, and see why it matters. This supports **SDG 4, Quality Education**, by making learning more engaging and accessible."

## Slide 3 - The story and the player's goal (45 seconds)
**Say:**
"Alex comes home on a Tuesday afternoon and finds that his younger brother Sam is missing. A locked-down system called SENTINEL has taken control of the house. To follow the clues across six places, Alex must solve code locks. The player's goal is to learn enough to unlock every door and bring Sam home."
*(Show the storyboard image here.)*

## Slide 4 - How the game works (1 minute)
**Say:**
"The player explores a level in 2D, with a 3D look, which is why we call it 2.5D. There are two kinds of challenges. **Code locks** are doors the player chooses to open. **Hazards**, like lasers and patrol bots, catch the player and force a challenge that must be answered to escape. Every correct lock earns 100 points and a clue. Using a hint costs 25 points. Nobody stays stuck: after three wrong answers the hint is free and the game can show how the answer starts, and after five it can show the whole answer - the player still runs it and reads why it works, but earns no points for it."

## Slide 5 - Our learn-before-you-answer design (1.5 minutes)
**Say:**
"This is the core of our educational design. Before each challenge, the player sees a **learning card**: the concept, a definition, how it works, an example, and what the example does. Then the question appears.
If the answer is wrong, the game explains the mistake in that choice without revealing the correct answer, and the player can retry or press Review Concept to read the card again, without losing progress. If the answer is right, the game explains why.
Difficulty increases gradually: in the first half of the game, hazards ask only easy questions, and harder ones are mixed in later.
We cover 10 Python concepts: variables, data types, arithmetic, comparison, if/else, boolean logic, loops, lists, functions, and loops combined with if."

## Slide 6 - Content and safety (1 minute)
**Say:**
"The game is Python only, and every challenge is checked by running code. It has 32 Python challenges and 10 lesson cards. [The questions were reviewed by (name/role) - SAY THIS ONLY IF TRUE.]
There are two kinds of challenge. In **Run Code** challenges (every door, plus three bug traps, 24 in total), the player types the missing code, presses RUN to see what it prints, and presses CHECK; the game runs it against several hidden test cases, so typing the answer value doesn't pass. In the three bug traps, SENTINEL has already filled the blank with a bug, and the player has to find and fix it. In **Predict the Output** challenges (used by hazards, 8 in total), the player types what the code will print, and the game runs the code to check the prediction, then shows the real output.
For safety, the code never runs on the computer itself: it runs in a small Python interpreter we wrote inside the game, which understands beginner Python only, has no file, network or import access, and has limits on steps, time, output and memory, so an endless loop just stops with a friendly message. The content is fixed and managed by the developers, so there is no way to change questions or answers while playing.
PHP and SQL are planned for future versions; their content is kept in the project but not offered in this version."

## Slide 7 - What is done (1 minute)
**Say:**
"At about 70 percent we have:
- the full core loop: explore, learn, answer, feedback, clue and points;
- six levels with a level map and progression rules;
- 32 Python challenges, all checked by running code in our sandboxed interpreter, and 10 lessons;
- scoring, hints and a clue journal called Investigation Logs;
- nine doors, in every level, where the player's code visibly drives a device by the door (keypad, counters, gate lights, a function machine, a laser grid) - and wrong code shows its wrong result in red;
- a help ladder so no one gets stuck (free hint, then the start of the answer, then the answer, with no points for a shown answer);
- excitement: the camera zooms to each device as the player's code plays, a first-try streak badge, and a SENTINEL boss in the final level whose three shields break as the player solves the last three locks;
- SENTINEL bug traps, where the player fixes planted bugs (including an endless loop), an alarm escape after the last lock of the later levels (red edges, tense music and faster hazards, but coding is never timed), and a "Signal to Sam" meter that grows with each cleared level;
- 24 achievements and up to 3 stars per level, which reward learning (first tries, fixing code after feedback, playing without hints), never speed;
- game feel: Alex and SENTINEL reactions, flashes and shakes on answers and hazard catches, and a mission line for every level;
- saved progress, including Unity Cloud Save with anonymous sign-in;
- settings, the SDG 4 information screen, cutscenes, dialogue and audio;
- a redesigned main menu and level map."

## Slide 8 - Technical design (1 minute)
**Say:**
"We built the game in **Unity 6** with C#, the Universal Render Pipeline, and Unity's Cloud Save service, and we use GitHub for version control.
The design keeps data separate from the interface. Questions and lessons are data assets. One challenge manager runs every challenge, whether it comes from a lock or a hazard, so the rules for scoring and feedback exist in one place. Lessons are matched to questions by ID, and a validation check reports any question that has no lesson."

## Slide 9 - Assets and credits (45 seconds)
**Say:**
"Our characters, Alex and Sam, come from Mixamo. Our fonts, Orbitron and JetBrains Mono, are open source under the SIL Open Font License. [Audio: (source). 2D art: (source). 3D environment: (how it was made). Fill in truthfully.]
[AI use - say according to the school's policy, for example: We used an AI assistant to help write and refactor code and to draft lesson text. We reviewed, tested and integrated the work, and we can explain how each system works.]"

## LIVE DEMO (4 minutes) - see checklist below

## Slide 10 - What remains (the last 30 percent) (1 minute)
**Say:**
"For the final defense we will:
- run playtests with real students and adjust difficulty and wording;
- add a harder tier of questions and balance the levels;
- polish the interface, audio and animations;
- fix bugs found in playtesting, and test with real mouse, keyboard and different screen sizes;
- finish the documentation and the final build.
We will also measure learning with a short pre-test and post-test."

## Slide 11 - Closing (20 seconds)
**Say:**
"In short, we have a working game that teaches before it tests, explains mistakes without giving answers, and is safe by design. Thank you. We are ready for your questions."

---

## Demo checklist (rehearse twice; record a backup video)
1. Launch the game. Point out the main menu and its five options (about 15 s).
2. Select Level: the Python level map appears. Say "cleared, next and locked levels".
3. Open Level 1 and walk to the hallway door. Press E: the **learning card** appears. Read the title and one line aloud, then Continue.
4. Type a **wrong** line on purpose (for example score = 15), press RUN to see what it prints, then CHECK: show that the feedback explains what the code printed without giving the answer. Press **Review Concept**, then **Back to Challenge**, then Retry.
5. Type the **right** line (score = score + 5) and press CHECK: show the explanation, +100 points and the clue.
   (Second Run Code demo, Warehouse door: type price + quantity, RUN shows 9, CHECK explains it should print 14, then fix it with price * quantity.)
   After the window closes, point out the **hallway keypad** typing in the code your program printed: "the code the player wrote is what opens the door." (Every level has doors like this: counters, gate lights, a function machine and a laser grid. If you close a window after a wrong CHECK, the device shows the wrong result in red.)
6. Walk into a hazard (the vacuum or the tripwire): show CAUGHT, type the predicted output, and point out "The game ran the code. It printed: ..." before Alex is released.
7. Back on the level map, point out the stars on the cleared level, then open **Achievements** from the main menu to show the badges earned in the demo (for example Debugger and the Variables badge).
8. Open Investigation Logs to show the collected clue, and (optional) the Cloud Progress panel.
9. (Optional, if time allows) In the Sentinel Control Room, solve one lock and point out the camera visiting the SENTINEL core as a shield breaks. Then step on the glowing bug trap: press RUN on SENTINEL's line to show the endless loop being stopped safely, fix it to n = n - 1, and CHECK. Point out the "Signal to Sam" meter at the bottom left.
Tip: use a fresh save for the demo.

## Likely questions and short answers
- **Why only Python?** It is a common first language and readable for beginners, and it let us make every challenge truly execute the player's code. PHP and SQL are planned for future versions. [Adjust to your school's actual reason.]
- **How are the questions checked?** By running code. Run Code questions run the player's line in our own limited Python interpreter and compare the printed output with several hidden test cases. Predict the Output questions run the question's code and compare the real output with the player's prediction. The interpreter supports beginner Python only (variables, arithmetic, if, loops, lists, functions, print) and is not a full Python.
- **Can a player break the game by typing code?** The interpreter has no file, network or import access and stops after a fixed number of steps, output size, call depth and time (about half a second). We tested infinite loops, endless recursion, huge numbers and lists, and forbidden names; they all end with an error message. [Say how many automated tests pass when you rehearse: run the Test Runner first.]
- **How does a question get its lesson card?** Each lesson lists the IDs of the questions it teaches; the challenge manager looks up the lesson when a challenge opens. If none is found it falls back to the topic.
- **How do you stop players from guessing?** Wrong-answer feedback never reveals the answer, hints cost points, and each question gives points only once.
- **What if a student is stuck?** After three wrong answers the hint is free and SHOW START gives the first part of the answer; after five, SHOW ANSWER types it in. The student still runs it and reads the explanation, but gets no points, badge or "no hints" star, so the help never pays more than solving.
- **Doesn't the alarm escape rush students?** No. Only the running is under pressure. Challenge windows pause the game, there is no timer, and being caught just opens the normal challenge.
- **How is progress saved?** Locally and in Unity Cloud Save with an anonymous ID; the game waits for the cloud to load before play starts.
- **How will you know students learn?** A short pre-test and post-test around a play session, plus the in-game scores and hint use.
- **What if there is no internet?** The cloud gate asks the player to connect and offers Retry. [If this is a concern for the panel, mention the plan for an offline mode as future work.]
- **Is the content appropriate for the target age?** Yes, it is school-appropriate and uses made-up names and story items.
- **Who made what?** [Name each member's part.]

## Rules for the day
- Only say what you can show. If something is untested, say "it is planned for the final".
- Know your own part well enough to explain it without the slides.
- If you are asked about AI, answer directly and honestly.
