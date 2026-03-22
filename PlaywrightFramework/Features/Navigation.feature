@navigation
Feature: Navigation
    As a user I want to navigate through the application
    so that I can access different pages and features.

    @smoke
    Scenario: Navigate to a page by path
        When I navigate to "/login"
        Then the page title should not be empty
        And the URL should contain "/login"

    Scenario: Navigate to a page and verify content
        When I navigate to "/login"
        Then the page should contain text "Login"

    Scenario Outline: Navigate to multiple pages
        When I navigate to "<path>"
        Then the URL should contain "<path>"

        Examples:
            | path       |
            | /login     |
            | /          |
