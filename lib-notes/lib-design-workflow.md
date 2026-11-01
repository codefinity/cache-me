# Workflow

The requirements will be written in design-claude\lib-requirements.md.

## 0. Skill 0: Analysis

Create a folder 0-analysis.
Requirements will be read from design-claude\lib-requirements.md
Grill and ask questions in the document.
I will write the answers to the questions in the document, and tell that I have answered the questions.
Claude will review the questions and ask more questions in the document till calude has everything it requires.

## 1. Skill 1: Designing the Configuration of the library

Create a folder 0-configuration.
Design the configuration of the library.
The congiguration will be used by the client application to configure the library.
Write the example configuration in configuration.md for me to review.
Every request for modification should create a new version of the .md document.

## 2. Skill 2: Designing the API Methods

Design the Interface and surface level class with dummy methods that will be visible on the client application.
Ask necessary questions.
Show the methods in the document.
Once approved, create those methods in the priject.
Every request for modification should create a new version of the .md document.

## 3. Skill 3: Wrire tests with Mocks

Create a testing project using XUnit and MOQ to write the tests for the methods of the class with empty methods.
Run the tests and see if they return intended results.

## 4. Skill 4: Create a component level diagram

Create a component level diagram in SVG format and embed it in .md file
Ask necessary questions.
The diagram must be simple.
Do not recommend a new feature.
Every request for modification should create a new version of the .md document.

## 5. Skill 5: Write Code

Write code for the complete component.

 

