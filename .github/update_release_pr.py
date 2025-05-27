import os
import requests

def get_github_prs(token, owner, repo, milestone, label, state):
    """
    Fetches pull requests from a GitHub repository that match a given milestone and label.

    Args:
        token (str): GitHub token.
        owner (str): The owner of the repository.
        repo (str): The name of the repository.
        milestone (str): The milestone title.
        label (str): The label name.
        state (str): State of PR, e.g. open

    Returns:
        list: A list of dictionaries, where each dictionary represents a pull request.
              Returns an empty list if no PRs are found or an error occurs.
    """
    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json",
    }
    
    milestone_id = None
    milestone_url = f"https://api.github.com/repos/{owner}/{repo}/milestones"
    params = {"state": state}
    
    try:
        response = requests.get(milestone_url, headers=headers, params=params)
        response.raise_for_status()
        milestones = response.json()
        for ms in milestones:
            if ms["title"] == milestone:
                milestone_id = ms["number"]
                break
        
        if not milestone_id:
            print(f"Milestone '{milestone}' not found in repository '{owner}/{repo}'.")
            return []

    except requests.exceptions.RequestException as e:
        print(f"Error fetching milestones: {e}")
        return []

    prs_url = f"https://api.github.com/repos/{owner}/{repo}/pulls"
    params = {
        "state": state,
        "milestone": milestone_id,
        "labels": label,
    }

    all_prs = []
    page = 1
    while True:
        try:
            params["page"] = page
            response = requests.get(prs_url, headers=headers, params=params)
            response.raise_for_status() # Raise an exception for HTTP errors
            prs = response.json()
            
            if not prs:
                break # No more PRs to fetch

            all_prs.extend(prs)
            page += 1

        except requests.exceptions.RequestException as e:
            print(f"Error fetching pull requests: {e}")
            break
            
    return all_prs

if __name__ == "__main__":
    github_token = os.environ.get("GITHUB_TOKEN") 
    
    if not github_token:
        print("Error: GITHUB_TOKEN environment variable not set.")
        exit(1)

    repository_owner = "flow-launcher"
    repository_name = "flow.launcher"
    target_milestone = "1.20.0"
    target_label = "enhancement"
    state = "closed"

    print(f"Fetching PRs for {repository_owner}/{repository_name} with milestone '{target_milestone}' and label '{target_label}'...")
    
    pull_requests = get_github_prs(
        github_token, 
        repository_owner, 
        repository_name, 
        target_milestone, 
        target_label,
        state
    )

    if pull_requests:
        print(f"\nFound {len(pull_requests)} pull requests:")
        for pr in pull_requests:
            print(f"- {pr['state']} #{pr['number']}: {pr['title']} (URL: {pr['html_url']})")
    else:
        print("No matching pull requests found.")
