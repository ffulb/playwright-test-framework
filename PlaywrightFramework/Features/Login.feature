@login
Feature: Login
    As a user I want to log in to the application
    so that I can access protected features.

    Background:
        Given I am on the login page

    @smoke
    Scenario: Successful login with valid credentials
        When I enter valid credentials
        And I click the login button
        Then I should be logged in successfully

    Scenario: Failed login with invalid credentials
        When I enter username "invalid_user" and password "bad_password"
        And I click the login button
        Then I should see a login error message

    Scenario: Login and logout
        When I enter valid credentials
        And I click the login button
        Then I should be logged in successfully
        When I click the logout button
        Then I should be on the login page

    Scenario: Login with empty credentials
        When I enter username "" and password ""
        And I click the login button
        Then I should see a login error message
