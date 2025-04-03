PoDebateRap Application Project Plan
Application Overview
PoDebateRap is an innovative web application that brings the dynamic world of hip-hop culture into the realm of AI-powered debates. The platform enables users to select two rappers from a curated list of 20 iconic artists who will engage in a battle of wits and wordplay on user-selected topics. Leveraging Azure OpenAI's GPT-3.5 model, each AI rapper adopts the distinctive persona, vernacular, and philosophical stance of their real-world counterpart, creating an authentic and engaging debate experience.

The core concept revolves around stylistic fidelity to each rapper's unique voice while maintaining substantive arguments on the selected debate topic. When users initiate a debate, they choose two rappers from dropdown menus and select a topic from a predefined list or create their own. The application then orchestrates a back-and-forth exchange limited to 100 characters per turn and 5 turns per rapper. This constraint encourages concise, impactful statements reminiscent of actual rap battles.

What sets PoDebateRap apart is its multisensory approach. Each rapper's arguments are not only generated as text but also converted to speech using Azure AI services, with voice characteristics that match the selected artist's tone and cadence. Visual cues, including blinking indicators, clearly denote which rapper is currently speaking. The debates unfold in real-time, creating a captivating audio-visual experience that bridges technology and hip-hop culture.

The application includes a competitive element through its voting and leaderboard system. After each debate concludes, users anonymously vote for the rapper they believe presented the most compelling arguments. These results are stored in Azure Table Storage, building win/loss records for each rapper that are displayed on a dedicated leaderboard page. This gamification aspect adds an extra layer of engagement and provides interesting data on which rappers' AI personas resonate most with users.

PoDebateRap is designed with a clean, hip-hop inspired interface that prioritizes ease of use while maintaining visual appeal. The application is built using Blazor Server, ensuring responsive performance and seamless integration with Azure services. By combining cutting-edge AI technology with hip-hop's rhetorical traditions, PoDebateRap creates a unique platform that entertains, engages, and perhaps even enlightens users about the art of debate through the lens of rap culture.

Detailed Requirements
Functional Requirements
Rapper Selection
System must maintain a database of 20 predefined rappers
Users must be able to select different rappers from dropdown menus on left and right sides
System must prevent selection of the same rapper in both positions
Debate Topics
System must store at least 20 predefined debate topics in Azure Table Storage
Topics must be retrievable and selectable through the UI
Character count must be displayed when users enter custom topics
AI Debate Generation
System must use Azure OpenAI GPT-3.5 to generate debate content
AI must adopt the persona of each selected rapper based on their style and lyrics
Each turn must be limited to 100 characters
Each rapper must have 5 turns per debate
Rappers should occasionally reference their own lyrics when relevant to debate
Text-to-Speech
System must convert generated text to speech using Azure AI services
Different voice characteristics should match each rapper's style
Audio must play sequentially as each rapper takes their turn
Visual Indicators
System must display a blinking light indicator showing which rapper is speaking
CSS transition effects must indicate when a rapper's turn ends
Voting System
System must display a modal dialog for voting after debate completion
Users must be able to select one rapper as the winner
System must record votes anonymously
Win/loss records must be stored in Azure Table Storage
Leaderboard
System must display a leaderboard of top 10 rappers by win percentage
Leaderboard must refresh when accessed from main page
Non-Functional Requirements
Performance
Application response time must be under 2 seconds for all operations
Text-to-speech conversion must maintain natural conversation pacing
UI/UX
Interface must have hip-hop inspired aesthetics
Layout must be intuitive and responsive
Visual transitions must be smooth and engaging
Scalability
System must handle multiple concurrent users
Azure Table Storage must efficiently store and retrieve rapper and debate data
Deployment
Application must be deployable to Azure App Service
Configuration must support standard CI/CD pipelines
Data Storage Requirements
Azure Table Storage Schema
Rappers Table
PartitionKey: "Rapper"
RowKey: [RapperID]
Properties:
Name: string
Wins: int
Losses: int
TotalDebates: int
Debate Topics Table
PartitionKey: "Topic"
RowKey: [TopicID]
Properties:
Title: string
Description: string
Page Designs and Functionality
1. Main Page (Home.razor)
The Main Page serves as the primary interface for the PoDebateRap application, featuring a hip-hop inspired design with graffiti-style fonts, urban color schemes, and visual elements that evoke rap battle culture. The page is divided into three main sections:

Top Section - Selection Area:

Two dropdown menus positioned on left and right sides of the screen for rapper selection
Each dropdown contains the names of all 20 rappers in alphabetical order
A central topic selection dropdown containing the 20 predefined topics
Optional text input field for custom topics with character counter
"Start Debate" button with animated hover effect
Middle Section - Debate Arena:

Two rapper profiles displayed on opposite sides of the screen
Rapper names prominently displayed in stylized font
Visual boxes around each rapper's area that highlight and blink when that rapper is speaking
Text display area showing the current turn's content
Progress indicator showing current turn number out of total turns
Animated transitions between turns with slide effects
Bottom Section - Controls:

Pause/Resume button for the debate
Volume control for the text-to-speech audio
Link to the Leaderboard page
About/Info button that displays brief application information in a modal
When users select two different rappers and a topic (or create their own), then click "Start Debate," the application calls Azure OpenAI to generate the first turn's content, converts it to speech, and begins the debate sequence. As each rapper completes their turn, the application visually transitions to the other rapper, generates new content, and continues the pattern for 5 turns each.

After all 10 turns (5 per rapper) are completed, a modal dialog appears asking "Who won this debate?" with buttons for each rapper. Upon selection, the result is recorded in Azure Table Storage, and users can either start a new debate or navigate to the leaderboard.

2. Leaderboard Page (Leaderboard.razor)
The Leaderboard Page displays performance statistics for all rappers in the system, with a focus on the top performers. The design maintains the hip-hop aesthetic while presenting data clearly and effectively.

Top Section - Header:

"PoDebateRap Leaderboard" title in graffiti-style font
Subtitle: "Top Rap Battle Champions"
"Return to Debates" navigation button
Last updated timestamp
Middle Section - Top 10 Leaderboard:

Visually prominent table displaying top 10 rappers by win percentage
Columns include:
Rank (with crown icon for #1)
Rapper Name
Wins
Losses
Total Debates
Win Percentage
Each row features subtle highlight effects on hover
Gold/silver/bronze styling for top three positions
Bottom Section - Complete Rankings:

Expandable section showing all 20 rappers' statistics
Same column structure as the top 10 table
Sorted by win percentage by default
Option to sort by different columns
When a user navigates to this page, the application retrieves current statistics from Azure Table Storage, calculates rankings, and refreshes the display. The page does not automatically update while being viewed - it only refreshes on page load to avoid confusion during viewing.

3. Debate Results Modal (ResultsModal.razor)
The Results Modal is a component that appears after a debate concludes, overlaying the main page with a semi-transparent background to focus attention on the voting interface.

Modal Content:

"Who Won This Debate?" header text
Brief instructional text: "Vote for the rapper with the most compelling arguments"
Two large, clickable rapper profile sections (left and right)
Each profile includes:
Rapper name
Current win/loss record
"Vote" button
"Skip Voting" option in smaller text at bottom
When a user selects a winner, the modal displays a brief "Vote Recorded" confirmation message, updates the database, then closes automatically, returning to the main page. If "Skip Voting" is selected, the modal simply closes without recording a vote.

4. Shared Components
NavMenu Component (NavMenu.razor):

Minimal navigation bar with application logo
Links to Main Page and Leaderboard
Responsive design that collapses on smaller screens
AudioPlayer Component (AudioPlayer.razor):

Hidden audio element that plays the text-to-speech output
Queue management for sequential playback
Event handling for turn transitions
DebateVisualizer Component (DebateVisualizer.razor):

Handles the visual representation of the debate
Controls the blinking indicator lights
Manages CSS transitions between turns
Displays debate text content
Integration Requirements
Azure OpenAI Integration:
Service connection configured in appsettings.json
API calls structured to provide rapper context and debate topic
System prompt engineered to guide the AI in adopting rapper personas
Response handling with error management
Azure Text-to-Speech Integration:
Voice selection based on rapper characteristics
Audio stream handling for sequential playback
Configuration for appropriate speaking rate and tone
Azure Table Storage Integration:
Connection string configuration
Data access service implementation
CRUD operations for rapper statistics and debate topics
Optimized query patterns for leaderboard display