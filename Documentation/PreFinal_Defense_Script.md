# CODE ESCAPE: THE MISSING KID - 70% Pre-Final Defense: What to Say

Target length: about 12 minutes (8 min talk + 4 min demo), then questions.
Text in [brackets] must be filled in by the team with true information before the defense.

---

## Slide 1 - Title (20 seconds)
**Say:**
"Good [morning/afternoon], panel. We are [Group name]. Our capstone is **Code Escape: The Missing Kid**, an educational 2.5D adventure game that teaches beginners Python, PHP and SQL. Today we present our progress at about 70 percent."

## Slide 2 - The problem and our goal (45 seconds)
**Say:**
"Many beginners find programming abstract and boring, and they give up early. Our game puts coding inside a story, so students learn a concept, use it to solve a puzzle, and see why it matters. This supports **SDG 4, Quality Education**, by making learning more engaging and accessible."

## Slide 3 - The story and the player's goal (45 seconds)
**Say:**
"Alex comes home on a Tuesday afternoon and finds that his younger brother Sam is missing. A locked-down system called SENTINEL has taken control of the house. To follow the clues across six places, Alex must solve code locks. The player's goal is to learn enough to unlock every door and bring Sam home."
*(Show the storyboard image here.)*

## Slide 4 - How the game works (1 minute)
**Say:**
"The player explores a level in 2D, with a 3D look, which is why we call it 2.5D. There are two kinds of challenges. **Code locks** are doors the player chooses to open. **Hazards**, like lasers and patrol bots, catch the player and force a challenge that must be answered to escape. Every correct lock earns 100 points and a clue. Using a hint costs 25 points."

## Slide 5 - Our learn-before-you-answer design (1.5 minutes)
**Say:**
"This is the core of our educational design. Before each challenge, the player sees a **learning card**: the concept, a definition, how it works, an example, and what the example does. Then the question appears.
If the answer is wrong, the game explains the mistake in that choice without revealing the correct answer, and the player can retry or press Review Concept to read the card again, without losing progress. If the answer is right, the game explains why.
Difficulty increases gradually: in the first half of the game, hazards ask only easy questions, and harder ones are mixed in later.
We cover 30 concepts across three languages, for example variables, loops and functions in Python and PHP, and SELECT, WHERE and GROUP BY in SQL."

## Slide 6 - Content and safety (1 minute)
**Say:**
"The game has 48 predefined questions, 16 per language, and 30 lesson cards. [The questions were reviewed by (name/role) - SAY THIS ONLY IF TRUE.]
For safety, the game **never runs code typed by the player**. Answers are checked against stored solutions. SQL exercises are simulations with made-up tables, so no real database is touched. The content is fixed and managed by the developers, so there is no way to change questions or answers while playing."

## Slide 7 - What is done (1 minute)
**Say:**
"At about 70 percent we have:
- the full core loop: explore, learn, answer, feedback, clue and points;
- six levels with a level map and progression rules;
- three programming languages with 48 questions and 30 lessons;
- scoring, hints and a clue journal called Investigation Logs;
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
2. Play, then choose Python. The level map appears: say "cleared, next and locked levels".
3. Open Level 1 and walk to the hallway door. Press E: the **learning card** appears. Read the title and one line aloud, then Continue.
4. Choose a **wrong** answer on purpose: show that the feedback explains the mistake without giving the answer. Press **Review Concept**, then **Back to Challenge**, then Retry.
5. Choose the **right** answer: show the explanation, +100 points and the clue.
6. Walk into a hazard (the vacuum or the tripwire): show CAUGHT, answer, and Alex is released.
7. Open Investigation Logs to show the collected clue, and (optional) the Cloud Progress panel.
Tip: use a fresh save for the demo, and have the same steps ready in SQL in case they ask to see another language.

## Likely questions and short answers
- **Why Python, PHP and SQL?** They are common beginner languages in our curriculum and cover general programming, web, and data. [Adjust to your school's actual reason.]
- **How are the questions checked?** Each question stores the correct choice or accepted answers; the game compares the player's answer to them. No code is executed.
- **How does a question get its lesson card?** Each lesson lists the IDs of the questions it teaches; the challenge manager looks up the lesson when a challenge opens. If none is found it falls back to the topic.
- **How do you stop players from guessing?** Wrong-answer feedback never reveals the answer, hints cost points, and each question gives points only once.
- **How is progress saved?** Locally and in Unity Cloud Save with an anonymous ID; the game waits for the cloud to load before play starts.
- **How will you know students learn?** A short pre-test and post-test around a play session, plus the in-game scores and hint use.
- **What if there is no internet?** The cloud gate asks the player to connect and offers Retry. [If this is a concern for the panel, mention the plan for an offline mode as future work.]
- **Is the content appropriate for the target age?** Yes, it is school-appropriate; the SQL tables use made-up data such as pets and sightings.
- **Who made what?** [Name each member's part.]

## Rules for the day
- Only say what you can show. If something is untested, say "it is planned for the final".
- Know your own part well enough to explain it without the slides.
- If you are asked about AI, answer directly and honestly.
